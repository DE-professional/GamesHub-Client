using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class BlackJackManager : MonoBehaviour
{
    [Header("Animation Settings")]
    public Transform deckSpawnPoint; // Assign a UI transform or Empty Object representing the shoe/deck
    public float moveDuration = 0.3f;   // Seconds per card move
    
    
    [Header("Assets & Prefabs")]
    public CardAtlas cardAtlas;
    public GameObject cardPrefab;
    public Transform playerHandContainer;
    public Transform splitHandContainer; // Second player card row, only shown after a split
    public Transform dealerHandContainer;

    [Header("UI Text & Buttons")]
    public TextMeshProUGUI playerText;
    public TextMeshProUGUI dealerText;
    public TextMeshProUGUI statusText;
    public Button hitButton;
    public Button standButton;
    public Button doubleDownButton;
    public Button splitButton;
    public Button startNewGameButton;
    public Button returnToMenuButton; // Shown once the session is over

    [Header("Betting")]
    public int startingChips = 500;   // Daily allowance
    public int handsPerSession = 10;
    public GameObject bettingPanel;   // Parent of the chip, clear and deal buttons
    public Button dealButton;
    public TextMeshProUGUI chipsText;
    public TextMeshProUGUI betText;
    public TextMeshProUGUI handsText;
    public BetStack betStack;         // Optional: chips fly onto the table as the bet goes up
    
    private int chips;
    private int currentBet;
    private int handsPlayed;
    
    private class PlayerHand
    {
        public List<Card> Cards = new List<Card>();
        public Transform Container;
        public int Bet;
        public bool IsDoubled;
        public bool IsDone;        // No more decisions for this hand (stood, doubled, bust, 21, split aces)
        public EndState? Result;   // Set early on bust, otherwise after the dealer plays
    }

    private List<PlayerHand> playerHands = new List<PlayerHand>();
    private int activeHandIndex;
    private PlayerHand ActiveHand => playerHands[activeHandIndex];
    private bool IsSplit => playerHands.Count > 1;

    private List<Card> dealerHand =  new List<Card>();
    private List<Card> deck = new List<Card>();
    
    private CardDisplay dealerHoleCardDisplay;
    private Card dealerHoleCard;
    private bool isHoleCardHidden = false;

    private string date;
    private System.Random random;

    private void Start()
    {
        date = DailyPuzzle.TodayUtc();
        random = new System.Random(DailyPuzzle.Seed("blackjack", date));
        if (splitHandContainer == null)
            Debug.LogWarning("BlackJackManager: splitHandContainer is not assigned, so splitting is disabled.", this);

        StartNewSession();
    }

    void StartNewSession()
    {
        chips = startingChips;
        handsPlayed = 0;
        BeginBetting();
    }

    void BeginBetting()
    {
        ClearHandContainers();
        DisableUIText();
        statusText.text = "Place your bet";
        currentBet = 0;
        if (betStack != null) betStack.Clear();
        playerHands.Clear();

        SetActionButtonsInteractable(false);
        startNewGameButton.gameObject.SetActive(false);
        if (returnToMenuButton != null) returnToMenuButton.gameObject.SetActive(false);
        bettingPanel.SetActive(true);
        dealButton.gameObject.SetActive(true);
        UpdateBettingUI();
    }

    public void MaxBet()
    {
        if (!bettingPanel.activeSelf) return;
        SetBet(Mathf.Min(currentBet + chips, chips));
    }
    // Hook chip buttons up to this with the chip value as the argument
    public void AddToBet(int amount)
    {
        if (!bettingPanel.activeSelf) return;
        SetBet(Mathf.Min(currentBet + amount, chips));
    }

    public void ClearBet()
    {
        if (!bettingPanel.activeSelf) return;
        currentBet = 0;
        if (betStack != null) betStack.Clear();
        UpdateBettingUI();
    }

    void SetBet(int bet)
    {
        if (betStack != null) betStack.Add(bet - currentBet);
        currentBet = bet;
        UpdateBettingUI();
    }

    public void OnDealButtonClicked()
    {
        if (currentBet <= 0) return;

        chips -= currentBet;
        handsPlayed++;
        bettingPanel.SetActive(false);
        dealButton.gameObject.SetActive(false);
        UpdateBettingUI();
        StartCoroutine(StartNewGameSequence());
    }

    public void OnNextHandButtonClicked()
    {
        BeginBetting();
    }

    void UpdateBettingUI()
    {
        chipsText.text = $"Chips: {chips}";
        betText.text = $"Bet: {(playerHands.Count > 0 ? TotalWagered() : currentBet)}";
        handsText.text = $"Hands left: {handsPerSession - handsPlayed}";
        dealButton.interactable = currentBet > 0;
    }

    int TotalWagered()
    {
        int total = 0;
        foreach (PlayerHand hand in playerHands) total += hand.Bet;
        return total;
    }

    bool IsSessionOver()
    {
        return handsPlayed >= handsPerSession || chips <= 0;
    }

    public void DisableUIText()
    {
        playerText.text = "";
        dealerText.text = "";
    }
    IEnumerator StartNewGameSequence()
    {
        DisableUIText();
        CreateDeck();
        ShuffleDeck();
        ClearHandContainers();

        PlayerHand firstHand = new PlayerHand { Container = playerHandContainer, Bet = currentBet };
        playerHands.Clear();
        playerHands.Add(firstHand);
        activeHandIndex = 0;
        dealerHand.Clear();

        statusText.text = "";
        SetActionButtonsInteractable(false);
        startNewGameButton.gameObject.SetActive(false);

        // Sequential deal with dramatic pauses
        yield return DealCardAnimated(firstHand.Cards, playerHandContainer);
        yield return new WaitForSeconds(0.15f);

        yield return DealCardAnimated(dealerHand, dealerHandContainer, isFaceDown: false);
        yield return new WaitForSeconds(0.15f);

        yield return DealCardAnimated(firstHand.Cards, playerHandContainer);
        yield return new WaitForSeconds(0.15f);

        yield return DealCardAnimated(dealerHand, dealerHandContainer, isFaceDown: true);

        UpdateUI();

        EndState? naturalResult = CheckBlackjack();
        if (naturalResult.HasValue)
        {
            firstHand.Result = naturalResult.Value;
            yield return EndGame();
            yield break;
        }

        yield return ContinuePlayerTurn();
    }

    // Returns the round result if either side was dealt a natural, otherwise null (play continues)
    private EndState? CheckBlackjack()
    {
        bool playerBlackJack = IsBlackJack(playerHands[0].Cards);
        bool dealerBlackJack = IsBlackJack(dealerHand);

        if (playerBlackJack && dealerBlackJack) return EndState.Push;
        if (playerBlackJack) return EndState.PlayerBlackJack;
        if (dealerBlackJack) return EndState.DealerBlackJack;
        return null;
    }

    private bool IsBlackJack(List<Card> hand)
    {
        return hand.Count == 2 && CalculateHandScore(hand) == 21;
    }

    void SetActionButtonsInteractable(bool interactable)
    {
        hitButton.interactable = interactable;
        standButton.interactable = interactable;
        doubleDownButton.interactable = interactable && CanDoubleDown();
        splitButton.interactable = interactable && CanSplit();
    }

    bool CanDoubleDown()
    {
        if (activeHandIndex >= playerHands.Count) return false;
        PlayerHand hand = ActiveHand;
        return !hand.IsDone && hand.Cards.Count == 2 && chips >= hand.Bet;
    }

    // One split per round, on any two cards of equal value (e.g. K + 10)
    bool CanSplit()
    {
        if (splitHandContainer == null || IsSplit || activeHandIndex >= playerHands.Count) return false;
        PlayerHand hand = ActiveHand;
        return !hand.IsDone
               && hand.Cards.Count == 2
               && hand.Cards[0].GetValue() == hand.Cards[1].GetValue()
               && chips >= hand.Bet;
    }

    // Moves to the next hand that still needs decisions, or hands over to the dealer once all are done
    IEnumerator ContinuePlayerTurn()
    {
        while (activeHandIndex < playerHands.Count && ActiveHand.IsDone) activeHandIndex++;

        UpdateHandHighlights();
        UpdateUI();

        if (activeHandIndex < playerHands.Count)
        {
            SetActionButtonsInteractable(true);
            yield break;
        }

        yield return DealerTurnSequence();
    }

    // Marks the hand finished if it bust or reached 21, so the player isn't asked to act on it
    void CheckHandFinished(PlayerHand hand)
    {
        int score = CalculateHandScore(hand.Cards);
        if (score > 21)
        {
            hand.Result = EndState.PlayerBust;
            hand.IsDone = true;
        }
        else if (score == 21)
        {
            hand.IsDone = true;
        }
    }

    // Dims the hand that isn't being played after a split
    void UpdateHandHighlights()
    {
        bool isPlayerTurn = activeHandIndex < playerHands.Count;
        foreach (PlayerHand hand in playerHands)
        {
            bool isDimmed = IsSplit && isPlayerTurn && hand != ActiveHand;
            GetContainerCanvasGroup(hand.Container).alpha = isDimmed ? 0.5f : 1f;
        }
    }

    CanvasGroup GetContainerCanvasGroup(Transform container)
    {
        CanvasGroup group = container.GetComponent<CanvasGroup>();
        if (group == null) group = container.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    void ClearHandContainers()
    {
        foreach (Transform child in playerHandContainer) Destroy(child.gameObject);
        foreach (Transform child in dealerHandContainer) Destroy(child.gameObject);
        GetContainerCanvasGroup(playerHandContainer).alpha = 1f;

        if (splitHandContainer != null)
        {
            foreach (Transform child in splitHandContainer) Destroy(child.gameObject);
            GetContainerCanvasGroup(splitHandContainer).alpha = 1f;
            splitHandContainer.gameObject.SetActive(false);
        }

        dealerHoleCardDisplay = null;
        dealerHoleCard = null;
        isHoleCardHidden = false;
    }

    void UpdateUI()
    {
        if (IsSplit)
        {
            List<string> handScores = new List<string>();
            for (int i = 0; i < playerHands.Count; i++)
            {
                string marker = i == activeHandIndex ? "> " : "";
                handScores.Add($"{marker}Hand {i + 1}: {CalculateHandScore(playerHands[i].Cards)}");
            }
            playerText.text = string.Join("   ", handScores);
        }
        else if (playerHands.Count == 1)
        {
            playerText.text = $"Player: {CalculateHandScore(playerHands[0].Cards)}";
        }

        // While the hole card is hidden, display only the value of dealer's visible first card
        if (isHoleCardHidden && dealerHand.Count >= 1)
        {
            dealerText.text = $"Dealer: {dealerHand[0].GetValue()}";
        }
        else
        {
            dealerText.text = $"Dealer: {CalculateHandScore(dealerHand)}";
        }
    }

    private  int CalculateHandScore(List<Card> hand)
    {
        int aceCount = 0;
        int handScore = 0;
        foreach (Card card in hand)
        {
            handScore += card.GetValue();
            if(card.Rank == Rank.Ace) aceCount++;
        }
        while (handScore > 21 && aceCount > 0)
        {
            handScore -= 10;
            aceCount--;
        }

        return handScore;
    }

    
    
    void RevealDealerHoleCard()
    {
        if (isHoleCardHidden && dealerHoleCardDisplay != null)
        {
            // Flip the face-down card to face-up using its actual Card data
            dealerHoleCardDisplay.DisplayCard(dealerHoleCard, cardAtlas, isFaceDown: false);
            isHoleCardHidden = false;
        }
    }
    void CreateDeck()
    {
        deck.Clear();
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                Card card = new Card(suit, rank);
                deck.Add(card);
            }
        }
    }
    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = random.Next(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    Card DrawCard()
    {
        Card drawn = deck[0];
        deck.RemoveAt(0);
        return drawn;
    }

    public void OnHitButtonClicked()
    {
        StartCoroutine(PlayerHitSequence());
    }
    IEnumerator PlayerHitSequence()
    {
        // Lock input until the card has landed so the player can't spam hits mid-animation
        SetActionButtonsInteractable(false);

        PlayerHand hand = ActiveHand;
        yield return DealCardAnimated(hand.Cards, hand.Container);
        UpdateUI();

        // Bust ends the hand; 21 also ends it since there's nothing to gain by hitting
        CheckHandFinished(hand);
        yield return ContinuePlayerTurn();
    }
    
    public IEnumerator RevealDealerHoleCardAnimated()
    {
        if (isHoleCardHidden && dealerHoleCardDisplay != null)
        {
            yield return dealerHoleCardDisplay.AnimateFlip(dealerHoleCard, cardAtlas, duration: 0.4f);
            isHoleCardHidden = false;
            UpdateUI();
        }
    }
    IEnumerator DealCardAnimated(List<Card> targetHand, Transform container, bool isFaceDown = false)
    {
        Card card = DrawCard();
        targetHand.Add(card);

        GameObject newCardObj = Instantiate(cardPrefab, container);
        CardDisplay cardDisplay = newCardObj.GetComponent<CardDisplay>();
        cardDisplay.DisplayCard(card, cardAtlas, isFaceDown);

        if (isFaceDown)
        {
            dealerHoleCard = card;
            dealerHoleCardDisplay = cardDisplay;
            isHoleCardHidden = true;
        }

        // Force layout update so container calculates final card position immediately
        LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());

        // Slide card from deckSpawnPoint to layout position
        Vector3 spawnPos;
        if (deckSpawnPoint != null)
            spawnPos = deckSpawnPoint.position;
        else
            spawnPos = container.position + new Vector3(0, 300, 0);
        yield return cardDisplay.AnimateMoveFrom(spawnPos, moveDuration);
    }
    public void OnStandButtonClicked()
    {
        if (activeHandIndex >= playerHands.Count) return;
        SetActionButtonsInteractable(false);
        ActiveHand.IsDone = true;
        StartCoroutine(ContinuePlayerTurn());
    }

    IEnumerator DealerTurnSequence()
    {
        SetActionButtonsInteractable(false);
        UpdateHandHighlights();

        // 1. Reveal Hole Card with flip animation
        yield return RevealDealerHoleCardAnimated();
        yield return new WaitForSeconds(0.3f);

        // 2. Dealer draws step-by-step with delay, unless every player hand already bust
        if (playerHands.Exists(hand => !hand.Result.HasValue))
        {
            while (CalculateHandScore(dealerHand) < 17)
            {
                yield return DealCardAnimated(dealerHand, dealerHandContainer, isFaceDown: false);
                UpdateUI();
                yield return new WaitForSeconds(0.3f);
            }
        }

        foreach (PlayerHand hand in playerHands)
        {
            if (!hand.Result.HasValue) hand.Result = EvaluateWinner(hand);
        }

        yield return EndGame();
    }

    public void OnDoubleDownButtonClicked()
    {
        if (!CanDoubleDown()) return;
        StartCoroutine(DoubleDownSequence());
    }

    // Doubles the bet, takes exactly one more card, then the hand is finished
    IEnumerator DoubleDownSequence()
    {
        SetActionButtonsInteractable(false);

        PlayerHand hand = ActiveHand;
        chips -= hand.Bet;
        // The rack is hidden mid-hand, so the extra chips come from the chip count
        if (betStack != null) betStack.Add(hand.Bet, chipsText.transform);
        hand.Bet *= 2;
        hand.IsDoubled = true;
        UpdateBettingUI();

        yield return DealCardAnimated(hand.Cards, hand.Container);
        UpdateUI();

        CheckHandFinished(hand);
        hand.IsDone = true;
        yield return ContinuePlayerTurn();
    }

    public void OnSplitButtonClicked()
    {
        if (!CanSplit()) return;
        StartCoroutine(SplitSequence());
    }

    // Splits the pair into two hands with equal bets, then deals one new card to each
    IEnumerator SplitSequence()
    {
        SetActionButtonsInteractable(false);

        PlayerHand firstHand = ActiveHand;
        PlayerHand secondHand = new PlayerHand { Container = splitHandContainer, Bet = firstHand.Bet };
        chips -= secondHand.Bet;
        playerHands.Insert(activeHandIndex + 1, secondHand);
        UpdateBettingUI();

        // Move the second card (data and visual) over to the split row
        Card movedCard = firstHand.Cards[1];
        firstHand.Cards.RemoveAt(1);
        secondHand.Cards.Add(movedCard);

        splitHandContainer.gameObject.SetActive(true);
        Transform movedCardObj = firstHand.Container.GetChild(1);
        Vector3 oldPosition = movedCardObj.position;
        movedCardObj.SetParent(splitHandContainer, worldPositionStays: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(firstHand.Container.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(splitHandContainer.GetComponent<RectTransform>());
        UpdateUI();
        yield return movedCardObj.GetComponent<CardDisplay>().AnimateMoveFrom(oldPosition, moveDuration);

        yield return DealCardAnimated(firstHand.Cards, firstHand.Container);
        yield return new WaitForSeconds(0.15f);
        yield return DealCardAnimated(secondHand.Cards, secondHand.Container);

        // Split aces only get one card each
        bool splitAces = movedCard.Rank == Rank.Ace;
        foreach (PlayerHand hand in new[] { firstHand, secondHand })
        {
            CheckHandFinished(hand);
            if (splitAces) hand.IsDone = true;
        }

        yield return ContinuePlayerTurn();
    }

    EndState EvaluateWinner(PlayerHand hand)
    {
        int pScore = CalculateHandScore(hand.Cards);
        int dScore = CalculateHandScore(dealerHand);

        if (dScore > 21) return EndState.DealerBust;
        if (pScore > dScore) return EndState.PlayerWin;
        if (dScore > pScore) return EndState.DealerWin;
        return EndState.Push;
    }

    // Expects every player hand to have a Result
    IEnumerator EndGame()
    {
        SetActionButtonsInteractable(false);

        // Always show the dealer's full hand so the player can see why the round ended
        yield return RevealDealerHoleCardAnimated();
        UpdateUI();

        int net = 0;
        List<string> lines = new List<string>();
        for (int i = 0; i < playerHands.Count; i++)
        {
            PlayerHand hand = playerHands[i];
            int handNet = SettleBet(hand);
            net += handNet;

            string line = GetEndStateMessage(hand.Result.Value, hand);
            if (hand.IsDoubled) line = $"Doubled down. {line}";
            if (IsSplit) line = $"Hand {i + 1}: {line} ({FormatNetResult(handNet)})";
            lines.Add(line);
        }
        UpdateBettingUI();

        string message = $"{string.Join("\n", lines)}\n{FormatNetResult(net)}";

        if (IsSessionOver())
        {
            message += chips <= 0
                ? "\nYou're out of chips. Today's score: 0"
                : $"\nDay over! You cash out {chips} chips.";
            if (returnToMenuButton != null) returnToMenuButton.gameObject.SetActive(true);
        }
        else
        {
            startNewGameButton.gameObject.SetActive(true);
        }

        statusText.text = message;
    }

    // Pays out the bet (already deducted on deal) and returns the net change in chips for the hand
    int SettleBet(PlayerHand hand)
    {
        int bet = hand.Bet;
        int payout;
        switch (hand.Result.Value)
        {
            case EndState.PlayerBlackJack:
                payout = bet + bet * 3 / 2; // 3:2, rounded down
                break;
            case EndState.PlayerWin:
            case EndState.DealerBust:
                payout = bet * 2;
                break;
            case EndState.Push:
                payout = bet;
                break;
            default:
                payout = 0;
                break;
        }

        chips += payout;
        return payout - bet;
    }

    string FormatNetResult(int net)
    {
        if (net > 0) return $"+{net} chips";
        if (net < 0) return $"-{-net} chips";
        return "Bet returned";
    }

    string GetEndStateMessage(EndState endState, PlayerHand hand)
    {
        int pScore = CalculateHandScore(hand.Cards);
        int dScore = CalculateHandScore(dealerHand);

        switch (endState)
        {
            case EndState.PlayerBlackJack:
                return "Blackjack! You win!";
            case EndState.DealerBlackJack:
                return "Dealer has Blackjack. You lose...";
            case EndState.PlayerBust:
                return $"You bust with {pScore}. You lose...";
            case EndState.DealerBust:
                return $"Dealer busts with {dScore}. You win!";
            case EndState.PlayerWin:
                return $"Your {pScore} beats the dealer's {dScore}. You win!";
            case EndState.DealerWin:
                return $"Dealer's {dScore} beats your {pScore}. You lose...";
            case EndState.Push:
                if (IsBlackJack(hand.Cards) && IsBlackJack(dealerHand))
                    return "You and the dealer both have Blackjack. Push.";
                return $"You and the dealer both have {pScore}. Push.";
            default:
                return "";
        }
    }
    

}


public enum EndState
{
    Push,
    PlayerBust,
    DealerBust,
    PlayerBlackJack,
    DealerBlackJack,
    PlayerWin,
    DealerWin
}
