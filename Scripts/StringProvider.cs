using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RAXY.Utility.Localization
{
    [Serializable]
    public class StringProvider
    {
        public bool useLocalization;
        
        [HideIf("@useLocalization")]
        public string directString;

        [ShowIf("@useLocalization")]
        public LocalizationCacher stringLoc;
        
        /// <summary>
        /// If you're using localization, you have to register it first via LocalizationService
        /// </summary>
        public string String
        {
            get
            {
                if (useLocalization)
                    return stringLoc.CachedString;
                else
                    return directString;
            }
        }

        public async UniTask<string> GetStringAsync()
        {
            if (useLocalization)
            {
                if (!Application.isPlaying)
                    return await stringLoc.GetStringInternalAsync();

                return await LocalizationService.GetStringAsync(this);
            }
            else
                return directString;
        }

        public async UniTask<string> RefreshCacheAsync()
        {
            if (useLocalization)
            {
                if (!Application.isPlaying)
                    return await stringLoc.RefreshCacheInternalAsync();

                return await LocalizationService.RefreshCacheAsync(this);
            }
            else
                return directString;
        }
    }
}
