using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

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

        public LocalizedString localizedString;

        [ShowInInspector]
        [PropertyOrder(-1)]
        [DisplayAsString]
        [InlineButton("RefreshCacheAsync", SdfIconType.ArrowClockwise, "Refresh")]
        private string _cachedString = UNSET_STRING;

        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
        private bool _isCached;

        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
        private Locale _cachedLocale;

        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
        private TableEntryReference _cachedEntry;

        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
        private bool _cachingInProgress;

        public bool IsNull => Array.Exists(SpecialStates, s => s == _cachedString);
        private bool LocaleChanged => LocalizationSettings.SelectedLocale != _cachedLocale;
        private bool EntryChanged => localizedString?.TableEntryReference.KeyId != _cachedEntry.KeyId;

        // -----------------------------------------------------
        // Public API
        // -----------------------------------------------------

        /// <summary>
        /// This is not safe, there's a big chance the string isn't cached yet
        /// </summary>
        public string CachedString => _cachedString;

        /// <summary>
        /// Get the localized string, fetching from cache if possible.
        /// Automatically refreshes when locale or entry changes.
        /// </summary>
        public async UniTask<string> GetStringAsync()
        {
            if (localizedString == null)
            {
                ResetCache(UNSET_STRING);
                return _cachedString;
            }

            if (_cachingInProgress)
                return LOADING_STRING;

            // Check if we can reuse cache
            if (_isCached)
            {
                if (LocaleChanged)
                {
                    ResetCache(LOCALE_CHANGED);
                }
                else if (EntryChanged)
                {
                    ResetCache(ENTRY_CHANGED);
                }
                else
                {
                    return _cachedString; // Cache valid
                }
            }

            return await RefreshCacheAsync();
        }

        /// <summary>
        /// Forces re-fetch of the localized string.
        /// </summary>
        public async UniTask<string> RefreshCacheAsync()
        {
            if (localizedString == null)
            {
                ResetCache(UNSET_STRING);
                return _cachedString;
            }

            try
            {
                _cachingInProgress = true;
                _cachedString = LOADING_STRING;

                var handle = localizedString.GetLocalizedStringAsync();
                string localized = await handle.Task.AsUniTask();

                _cachedLocale = LocalizationSettings.SelectedLocale;
                _cachedEntry = localizedString.TableEntryReference;
                _cachedString = localized ?? NULL_STRING;
                _isCached = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalizationCacher] Failed to cache: {e.Message}");
                ResetCache(UNSET_STRING);
            }
            finally
            {
                _cachingInProgress = false;
            }

            return _cachedString;
        }

        /// <summary>
        /// Clears cached state and resets to default.
        /// </summary>
        public void ResetCache(string state = NULL_STRING)
        {
            _isCached = false;
            _cachedString = state;
            _cachedLocale = null;
            _cachedEntry = default;
        }

        // Optional: Useful helper
        public override string ToString() => _cachedString ?? NULL_STRING;
    }
}
