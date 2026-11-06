using UnityEngine;

namespace Util
{
    public static class Services
    {
        public static AuthService Auth {get; private set;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void init()
        {
            var api = new ApiClient("http://localhost:3900");
            Auth = new AuthService(api);
        }

    }
}