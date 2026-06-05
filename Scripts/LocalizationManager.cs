using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace RAXY.Utility.Localization
{
    public class LocalizationManager : MonoBehaviour
    {
        private static LocalizationManager _instance;

        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<LocalizationManager>();

                    if (_instance == null && Application.isPlaying)
                    {
                        var go = new GameObject(nameof(LocalizationManager));
                        _instance = go.AddComponent<LocalizationManager>();
                        DontDestroyOnLoad(go);
                    }

                    _instance?.SubscribeToLocaleChanges();
                }

                return _instance;
            }
        }

        public static event Action<Locale> OnLocaleChanged;

        private readonly HashSet<LocalizationCacher> _trackedCachers = new();
        private bool _isSubscribedToLocaleChanges;
        private bool _isChangingLocaleInternally;
        private bool _isRefreshingTrackedCaches;
        private UniTaskCompletionSource _refreshCompletionSource;

        [TitleGroup("Debug")]
        [ShowInInspector, ReadOnly]
        private int TrackedCacherCount => _trackedCachers.Count;

        [TitleGroup("Debug/Locale")]
        [ShowInInspector, ReadOnly]
        private string CurrentLocaleCode => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "<none>";

        [TitleGroup("Debug/Locale")]
        [ShowInInspector, ReadOnly]
        private string CurrentLocaleName => LocalizationSettings.SelectedLocale?.LocaleName ?? "<none>";

        [TitleGroup("Debug/Locale")]
        [SerializeField]
        [ValueDropdown(nameof(GetAvailableLocaleCodes))]
        private string testLocaleCode;

        [TitleGroup("Debug/Locale")]
        [Button("Change Language", ButtonSizes.Medium)]
        private void ChangeLanguageButton()
        {
            ChangeLanguageAsync(testLocaleCode).Forget();
        }

        [TitleGroup("Debug/Locale")]
        [Button("Refresh Tracked Caches", ButtonSizes.Medium)]
        private void RefreshTrackedCachesButton()
        {
            RefreshTrackedCachesAsync().Forget();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            SubscribeToLocaleChanges();
        }

        private void OnDestroy()
        {
            if (_isSubscribedToLocaleChanges)
            {
                LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
                _isSubscribedToLocaleChanges = false;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static async UniTask<string> GetStringAsync(LocalizationCacher cacher)
        {
            if (cacher == null)
                return LocalizationCacher.NULL_STRING;

            if (!Application.isPlaying)
                return await cacher.GetStringInternalAsync();

            Register(cacher);
            return await cacher.GetStringInternalAsync();
        }

        public static async UniTask<string> RefreshCacheAsync(LocalizationCacher cacher)
        {
            if (cacher == null)
                return LocalizationCacher.NULL_STRING;

            if (!Application.isPlaying)
                return await cacher.RefreshCacheInternalAsync();

            Register(cacher);
            return await cacher.RefreshCacheInternalAsync();
        }

        public static UniTask RefreshTrackedCachesAsync()
        {
            if (!Application.isPlaying || Instance == null)
                return UniTask.CompletedTask;

            return Instance.RefreshTrackedCachesInternalAsync();
        }

        public static async UniTask<bool> SetLocaleAsync(Locale locale)
        {
            if (locale == null)
                return false;

            await LocalizationSettings.InitializationOperation.Task.AsUniTask();

            var instance = Instance;
            if (LocalizationSettings.SelectedLocale == locale)
            {
                if (Application.isPlaying && instance != null)
                    await instance.RefreshTrackedCachesInternalAsync();

                return true;
            }

            if (!Application.isPlaying || instance == null)
            {
                LocalizationSettings.SelectedLocale = locale;
                OnLocaleChanged?.Invoke(locale);
                return true;
            }

            instance._isChangingLocaleInternally = true;
            try
            {
                LocalizationSettings.SelectedLocale = locale;
                await instance.RefreshTrackedCachesInternalAsync();
                OnLocaleChanged?.Invoke(locale);
            }
            finally
            {
                instance._isChangingLocaleInternally = false;
            }

            return true;
        }

        public static async UniTask<bool> SetLocaleAsync(string localeCode)
        {
            await LocalizationSettings.InitializationOperation.Task.AsUniTask();

            if (!TryGetLocale(localeCode, out var locale))
                return false;

            return await SetLocaleAsync(locale);
        }

        public static UniTask<bool> ChangeLanguageAsync(Locale locale)
        {
            return SetLocaleAsync(locale);
        }

        public static UniTask<bool> ChangeLanguageAsync(LocaleIdentifier localeIdentifier)
        {
            return SetLocaleAsync(localeIdentifier.Code);
        }

        public static UniTask<bool> ChangeLanguageAsync(SystemLanguage systemLanguage)
        {
            return SetLocaleAsync(new LocaleIdentifier(systemLanguage).Code);
        }

        public static UniTask<bool> ChangeLanguageAsync(string localeCode)
        {
            return SetLocaleAsync(localeCode);
        }

        public static void ChangeLanguage(Locale locale)
        {
            ChangeLanguageAsync(locale).Forget();
        }

        public static void ChangeLanguage(LocaleIdentifier localeIdentifier)
        {
            ChangeLanguageAsync(localeIdentifier).Forget();
        }

        public static void ChangeLanguage(SystemLanguage systemLanguage)
        {
            ChangeLanguageAsync(systemLanguage).Forget();
        }

        public static void ChangeLanguage(string localeCode)
        {
            ChangeLanguageAsync(localeCode).Forget();
        }

        public static bool TryGetLocale(string localeCode, out Locale locale)
        {
            locale = null;

            if (string.IsNullOrWhiteSpace(localeCode))
                return false;

            var availableLocales = LocalizationSettings.AvailableLocales;
            if (availableLocales == null)
                return false;

            locale = availableLocales.GetLocale(new LocaleIdentifier(localeCode));

            if (locale == null)
            {
                locale = availableLocales.Locales.FirstOrDefault(availableLocale =>
                    availableLocale != null &&
                    string.Equals(availableLocale.Identifier.Code, localeCode, StringComparison.OrdinalIgnoreCase));
            }

            return locale != null;
        }

        public static string GetCachedString(LocalizationCacher cacher, string fallback = null)
        {
            if (cacher == null)
                return fallback ?? LocalizationCacher.NULL_STRING;

            if (Application.isPlaying)
                Register(cacher);

            return cacher.CachedString ?? fallback ?? LocalizationCacher.NULL_STRING;
        }

        public static void Register(LocalizationCacher cacher)
        {
            if (cacher == null)
                return;

            if (!Application.isPlaying || Instance == null)
                return;

            Instance._trackedCachers.Add(cacher);
        }

        public static void Unregister(LocalizationCacher cacher)
        {
            if (cacher == null || _instance == null)
                return;

            _instance._trackedCachers.Remove(cacher);
        }

        public static void ClearTrackedCachers()
        {
            if (_instance == null)
                return;

            _instance._trackedCachers.Clear();
        }

        private void SubscribeToLocaleChanges()
        {
            if (_isSubscribedToLocaleChanges)
                return;

            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
            _isSubscribedToLocaleChanges = true;
        }

        private void HandleSelectedLocaleChanged(Locale locale)
        {
            if (_isChangingLocaleInternally)
                return;

            RefreshTrackedCachesAndNotifyAsync(locale).Forget();
        }

        private async UniTask RefreshTrackedCachesAndNotifyAsync(Locale locale)
        {
            await RefreshTrackedCachesInternalAsync();
            OnLocaleChanged?.Invoke(locale);
        }

        private async UniTask RefreshTrackedCachesInternalAsync()
        {
            if (_isRefreshingTrackedCaches && _refreshCompletionSource != null)
            {
                await _refreshCompletionSource.Task;
                return;
            }

            _isRefreshingTrackedCaches = true;
            _refreshCompletionSource = new UniTaskCompletionSource();

            try
            {
                var snapshot = new List<LocalizationCacher>(_trackedCachers);
                var tasks = new List<UniTask<string>>(snapshot.Count);

                foreach (var cacher in snapshot)
                {
                    if (cacher != null)
                        tasks.Add(cacher.RefreshCacheInternalAsync());
                }

                if (tasks.Count > 0)
                    await UniTask.WhenAll(tasks);

                _refreshCompletionSource.TrySetResult();
            }
            catch (Exception e)
            {
                _refreshCompletionSource.TrySetException(e);
                throw;
            }
            finally
            {
                _isRefreshingTrackedCaches = false;
                _refreshCompletionSource = null;
            }
        }

        private IEnumerable<ValueDropdownItem<string>> GetAvailableLocaleCodes()
        {
            var availableLocales = LocalizationSettings.AvailableLocales;
            if (availableLocales?.Locales == null)
                yield break;

            foreach (var locale in availableLocales.Locales)
            {
                if (locale == null)
                    continue;

                var code = locale.Identifier.Code;
                var label = string.IsNullOrEmpty(locale.LocaleName)
                    ? code
                    : $"{locale.LocaleName} ({code})";

                yield return new ValueDropdownItem<string>(label, code);
            }
        }
    }
}
