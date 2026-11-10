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
            text.SetText(def + session.user.displayName);
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
