using System;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine;

namespace RAXY.Utility.Localization
{
    [Serializable]
    public class LocalizationCacher
    {
        public const string UNSET_STRING = "<unset>";
        public const string NULL_STRING = "<null>";
        public const string LOADING_STRING = "<loading>";
        public const string LOCALE_CHANGED = "<locale-changed>";
        public const string ENTRY_CHANGED = "<entry-changed>";

        private static readonly string[] SpecialStates = 
        {
            UNSET_STRING, NULL_STRING, LOADING_STRING, LOCALE_CHANGED, ENTRY_CHANGED
        };

        public bool IsNull => Array.Exists(SpecialStates, s => s == Result);

        public LocalizedString localizedString;

        [PropertyOrder(-1)]
        [ShowInInspector, ReadOnly]
        public string Result
        {
            get
            {
                if (!_isCached || LocaleChanged || EntryChanged)
                {
                    DoCache();
                }

                return _cachedString;
            }
        }

        private bool LocaleChanged => LocalizationSettings.SelectedLocale != _cachedLocale;
        private bool EntryChanged => localizedString?.TableEntryReference.KeyId != _cachedEntry.KeyId;

        public void DoCache()
        {
            if (localizedString == null)
            {
                ResetCache(UNSET_STRING);
                return;
            }

            try
            {
#if UNITY_EDITOR
                _cachedString = localizedString.GetLocalizedString();
#else
                _cachedString = await localizedString.GetLocalizedStringAsync();
#endif

                _cachedLocale = LocalizationSettings.SelectedLocale;
                _cachedEntry = localizedString.TableEntryReference;
                _isCached = true;
            }
            catch
            {
                //Debug.LogWarning($"LocalizationCacher failed to cache: {e.Message}");
                ResetCache(UNSET_STRING);
            }
        }

        public void ResetCache(string state = NULL_STRING)
        {
            _isCached = false;
            _cachedString = state;
            _cachedLocale = null;
            _cachedEntry = default;
        }

        [FoldoutGroup("Debug"), NonSerialized, ShowInInspector, ReadOnly]
        private bool _isCached;

        [FoldoutGroup("Debug"), NonSerialized, ShowInInspector, ReadOnly]
        private Locale _cachedLocale;

        [FoldoutGroup("Debug"), NonSerialized, ShowInInspector, ReadOnly]
        private TableEntryReference _cachedEntry;

        [FoldoutGroup("Debug"), NonSerialized, ShowInInspector, ReadOnly]
        private string _cachedString;
    }
}
