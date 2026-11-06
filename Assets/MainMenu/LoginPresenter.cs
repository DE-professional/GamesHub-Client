using System;
using Util;

namespace Menu
{
    using UnityEngine;

    public class LoginPresenter : MonoBehaviour
    {
        [SerializeField] LoginDropdown view;

        void OnEnable()  => view.SubmitRequested += OnSubmit;
        void OnDisable() => view.SubmitRequested -= OnSubmit;

        async void OnSubmit(string email, string password)
        {
            view.SetBusy(true);
            LoginResult result = await Services.Auth.LoginAsync(email, password);

            if (this == null) return; // widget was destroyed while waiting (e.g. scene change)
            view.SetBusy(false);

            switch (result)
            {
                case LoginResult.Success:
                    view.ShowError("");
                    view.SetOpen(false);
                    break;
                case LoginResult.InvalidCredentials:
                    view.ShowError("Wrong email or password.");
                    break;
                case LoginResult.NetworkError:
                    view.ShowError("Can't reach the server. Check your connection.");
                    break;
                default:
                    view.ShowError("Something went wrong. Try again.");
                    break;
            }
        }
    }
}