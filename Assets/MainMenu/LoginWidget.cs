using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LoginDropdown : MonoBehaviour
{
    [SerializeField] Button toggleButton;
    [SerializeField] RectTransform panel;
    [SerializeField] RectTransform content;
    [SerializeField] TMP_InputField emailField;
    [SerializeField] TMP_InputField passwordField;
    [SerializeField] TMP_Text errorText;
    [SerializeField] Button submitButton;
    [SerializeField] float duration = 0.2f;

    public event Action<string, string> SubmitRequested;

    CanvasGroup group;
    Coroutine anim;
    bool open;

    void Awake()
    {
        group = panel.GetComponent<CanvasGroup>();

        toggleButton.onClick.AddListener(Toggle);
        submitButton.onClick.AddListener(Submit);
        emailField.onSubmit.AddListener(_ => Focus(passwordField)); // Enter in email -> password
        passwordField.onSubmit.AddListener(_ => Submit());           // Enter in password -> submit

        SetHeight(0f);
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;
    }

    void Update()
    {
        if (!open) return;
#if ENABLE_INPUT_SYSTEM
        bool tab = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        bool tab = Input.GetKeyDown(KeyCode.Tab);
#endif
        if (!tab) return;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == emailField.gameObject) Focus(passwordField);
        else if (selected == passwordField.gameObject) Focus(emailField);
    }

    public void Toggle() => SetOpen(!open);

    public void SetOpen(bool value)
    {
        open = value;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(value));
    }

    IEnumerator Animate(bool opening)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float startH = panel.sizeDelta.y;
        float endH = opening ? LayoutUtility.GetPreferredHeight(content) : 0f;
        float startA = group.alpha;
        float endA = opening ? 1f : 0f;

        if (!opening) group.interactable = group.blocksRaycasts = false;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            SetHeight(Mathf.Lerp(startH, endH, e));
            group.alpha = Mathf.Lerp(startA, endA, e);
            yield return null;
        }

        SetHeight(endH);
        group.alpha = endA;

        if (opening)
        {
            group.interactable = group.blocksRaycasts = true;
            Focus(emailField);
        }
        anim = null;
    }

    void Submit()
    {
        string email = emailField.text.Trim();
        string password = passwordField.text;

        if (email.Length == 0 || password.Length == 0)
        {
            ShowError("Enter your email and password.");
            return;
        }

        ShowError("");
        passwordField.text = ""; // don't leave the password sitting in the field
        SubmitRequested?.Invoke(email, password);
    }

    public void ShowError(string message)
    {
        errorText.text = message;
        // Height changed (error appeared/disappeared): resize to fit if open
        if (open && anim == null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            SetHeight(LayoutUtility.GetPreferredHeight(content));
        }
    }

    void Focus(TMP_InputField field)
    {
        field.Select();
        field.ActivateInputField();
    }

    void SetHeight(float h) => panel.sizeDelta = new Vector2(panel.sizeDelta.x, h);
    
    public void SetBusy(bool busy)
    {
        submitButton.interactable = !busy;
        emailField.interactable = !busy;
        passwordField.interactable = !busy;
    }
}