using System;
using Menu;
using TMPro;
using UnityEngine;
using Util;

public class SuccessText : MonoBehaviour
{
    public TextMeshProUGUI text;

    private void OnEnable()
    {
        Services.Auth.SessionChanged += OnSessionChanged;

        // SessionChanged only reports changes. Coming back to the menu from a game, the player is
        // still logged in and no event fires, so show the current state now.
        OnSessionChanged(Services.Auth.Current);
    }
    void OnDisable()
    {
        Services.Auth.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(Session session)
    {
        string def = "Logged in as ";
        if (session != null)
        {
            text.SetText(def + session.user.Tag); // full Name#1234: display names alone aren't unique
        }
        else
        {
            text.SetText(def + "Guest");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
