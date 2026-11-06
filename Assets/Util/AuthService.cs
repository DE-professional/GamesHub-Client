using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class Session
{
    public string token;
    public string displayName;
}
public enum LoginResult{Success, InvalidCredentials, NetworkError, ServerError}

[Serializable] public class User
{
    public string displayName;
    public int discriminator;
    public bool emailVerified;
}

public class AuthService
{
    [Serializable] struct LoginRequest
    {
        public string email;
        public string password;
    }
    
    [Serializable] struct LoginResponse
    {
        public string token; 
        public User user;
    }

    private readonly ApiClient api;
    public Session Current { get; private set; }
    
    public bool IsLoggedIn => Current != null;
    
    public event Action<Session> SessionChanged;

    public AuthService(ApiClient api)
    {
        this.api = api;
        api.Unauthorized += Logout;
    }

    public async Awaitable<LoginResult> LoginAsync(string email, string password)
    {
        var r = await api.Post<LoginResponse>("/auth/login", new LoginRequest { email = email, password = password }, authenticate: false);

        switch (r.Outcome)
        {
            case ApiOutcome.NetworkError:
                return LoginResult.NetworkError;
            case ApiOutcome.ParseError:
                return LoginResult.ServerError;
            case ApiOutcome.HttpError:
                return r.Status == 401 ? LoginResult.InvalidCredentials : LoginResult.ServerError;
        }
        
        Current = new Session { token = r.Data.token, displayName = r.Data.user.displayName };
        api.AuthToken = r.Data.token;
        SessionChanged?.Invoke(Current);
        return LoginResult.Success;
    }
    public void Logout()
    {
        Current = null;
        api.AuthToken = null;
        SessionChanged?.Invoke(null);
    }
}