using UnityEngine;

namespace Util
{
    /// <summary>
    /// Static service locator. Creates the networking services once, before the first
    /// scene loads, and exposes them game-wide.
    /// </summary>
    public static class Services
    {
        /// <summary>The game-wide <see cref="AuthService"/>. Safe to use from <c>Awake</c> onward.</summary>
        public static AuthService Auth {get; private set;}

        /// <summary>
        /// Builds the shared <see cref="ApiClient"/> and the services that use it. Called by
        /// Unity before any <c>Awake</c> in the first scene.
        /// </summary>
        /// <remarks>
        /// The base URL is hard-coded to the local development server. Construct any new
        /// service here with the same <c>api</c> instance so it shares the token and the
        /// <see cref="ApiClient.Unauthorized"/> event.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void init()
        {
            var api = new ApiClient("http://localhost:3900");
            Auth = new AuthService(api);
        }

    }
}
