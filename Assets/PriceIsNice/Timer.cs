using System;
using TMPro;
using UnityEngine;

public class Timer : MonoBehaviour
{
    public bool _timerActive;
    public float _currTime;
    public TextMeshProUGUI text;

    
    public void StartTimer()
    {
        _timerActive = true;
    }

    public float StopTimer()
    {
        _timerActive = false;
        return _currTime;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_timerActive)
        {
            _currTime += Time.deltaTime;
        }
        text.text = $"<size=40%><color={PriceIsNiceRetroUI.LabelColor}>TIME</color></size>\n{_currTime:F1}s";
    }
}
