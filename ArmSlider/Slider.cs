using MelonLoader;
using RumbleModdingAPI;
using UnityEngine;
using HarmonyLib;
using System;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Interactions.InteractionBase;
using System.Collections;
using System.Collections.Generic;

namespace ArmSlider
{
    public class Slider : MelonMod
    {
        public class ValueChangedEventArgs : EventArgs
        {
            public float Value { get; private set; }

            public ValueChangedEventArgs(float value)
            {
                Value = value;
            }
        }

        public class ActiveChangedEventArgs : EventArgs
        {
            public bool Active { get; private set; }

            public ActiveChangedEventArgs(bool active)
            {
                Active = active;
            }
        }

        // Use this class to listen for changes to the value or active state of a setting.
        // Changes made to Value or Active will be reflected by the slider.
        public class Setting
        {
            public float Min { get; private set; }
            public float Max { get; private set; }
            public int DecimalPlaces { get; private set; }
            private float value;
            public event EventHandler<ValueChangedEventArgs> ValueChanged;
            public float Value
            {
                get => value;
                set
                {
                    this.value = value;

                    if (activeOption == settingsReversed[this])
                        MelonCoroutines.Start(LateUpdateSlider(value));

                    ValueChanged?.Invoke(this, new ValueChangedEventArgs(value));
                }
            }
            private bool active;
            public event EventHandler<ActiveChangedEventArgs> ActiveChanged;
            public bool Active
            {
                get => active;
                set
                {
                    active = value;

                    RumbleModUI.ModSetting option = settingsReversed[this];
                    option.SavedValueChanged -= SliderChanged;
                    option.Value = value;
                    option.SavedValue = value;
                    if (sliderOptions.GetUIStatus())
                        RumbleModUI.UI.instance.ForceRefresh();
                    option.SavedValueChanged += SliderChanged;

                    if (value)
                    {
                        activeOption = option;
                        slider.SetActive(true);
                        MelonCoroutines.Start(LateUpdateSlider(this.value));
                    }
                    else if (activeOption == option)
                    {
                        activeOption = null;
                        if (slider != null)
                        {
                            slider.SetActive(false);
                        }
                    }

                    ActiveChanged?.Invoke(this, new ActiveChangedEventArgs(value));
                }
            }

            public Setting(float min, float max, int decimalPlaces, float value, bool active)
            {
                Min = min;
                Max = max;
                DecimalPlaces = decimalPlaces;
                this.value = value;
                this.active = active;
            }
        }

        private static GameObject templateSlider;
        private static GameObject slider;

        private static RumbleModUI.Mod sliderOptions = new RumbleModUI.Mod();
        private static RumbleModUI.ModSetting activeOption = null;
        private static Dictionary<RumbleModUI.ModSetting, Setting> settings = new Dictionary<RumbleModUI.ModSetting, Setting>();
        private static Dictionary<Setting, RumbleModUI.ModSetting> settingsReversed = new Dictionary<Setting, RumbleModUI.ModSetting>();

        // Stop left hand from interacting with the voice slider
        [HarmonyPatch(typeof(InteractionHand), "StartPreInteraction")]
        private static class InteractionHand_StartPreInteraction_Patch
        {
            private static bool Prefix(InteractionHand __instance, InteractionBase interaction)
            {
                if (__instance.gameObject == PlayerManager.instance.localPlayer.Controller.gameObject.transform.GetChild(1).GetChild(1).gameObject && interaction.transform.parent.name == "SettingSlider")
                    return false;
                return true;
            }
        }

        public override void OnLateInitializeMelon()
        {
            RumbleModUI.UI.instance.UI_Initialized += OnUIInit;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            sliderOptions.ModName = "ArmSlider";
            sliderOptions.ModVersion = "1.0.0";
            sliderOptions.SetFolder("ArmSlider");
            sliderOptions.SetLinkGroup(1, "");
        }

        public override void OnFixedUpdate()
        {
            if (Calls.Scene.GetSceneName() == "Gym" && templateSlider == null)
            {
                // Grab one of the sliders from the audio settings menu and use it as a template
                templateSlider = GameObject.Instantiate(Calls.GameObjects.Gym.Logic.SlabbuddyMenuVariant.MenuForm.Base.AudioSlab.GetGameObject().transform.GetChild(0).GetChild(0).gameObject);
                templateSlider.name = "TemplateSlider";
                templateSlider.SetActive(false);
                GameObject.DontDestroyOnLoad(templateSlider);
            }
            if (slider == null && templateSlider != null && PlayerManager.instance != null && PlayerManager.instance.localPlayer != null && PlayerManager.instance.localPlayer.Controller != null)
            {
                // Initialise the slider
                // Parent: Player Controller/Visuals/RIG/Bone_Pelvis/Bone_Spine/Bone_Chest/Bone_Shoulderblade_L/Bone_Shoulder_L/Bone_Lowerarm_L/
                slider = GameObject.Instantiate(templateSlider, PlayerManager.instance.localPlayer.Controller.gameObject.transform.GetChild(5).GetChild(7).GetChild(0).GetChild(2).GetChild(0).GetChild(1).GetChild(0).GetChild(0));
                slider.name = "SettingSlider";
                sliderOptions.GetFromFile();
                for (int i = 0; i < sliderOptions.Settings.Count; i++)
                {
                    if ((bool)sliderOptions.Settings[i].SavedValue)
                    {
                        activeOption = sliderOptions.Settings[i];
                        break;
                    }
                }
                slider.SetActive(activeOption != null);
                slider.transform.localPosition = new Vector3(0.02f, 0.08f, 0.02f);
                slider.transform.localRotation = Quaternion.Euler(355.0f, 320.0f, 273.0f);
                slider.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                MelonCoroutines.Start(LateUpdateSlider(activeOption == null ? 0 : settings[activeOption].Value));
                InteractionSlider interactionSlider = slider.GetComponentInChildren<InteractionSlider>();
                interactionSlider.OnNormalizedValueChanged.AddListener(new System.Action<float>(value =>
                {
                    if (activeOption != null)
                    {
                        Setting setting = settings[activeOption];
                        setting.Value = (float)Math.Round(setting.Min + value * (setting.Max - setting.Min), setting.DecimalPlaces);
                    }
                }));
                interactionSlider.interactionDistancePercentage = 0.6f;
                // This is necessary to prevent interference with moves
                interactionSlider.usePreInteractionLerps = false;
            }
        }

        private static void OnUIInit()
        {
            if (sliderOptions.Settings.Count == 0)
                sliderOptions.AddDescription("No slider options found", "", "No slider-compatible settings were found. Make sure you installed a mod that uses the slider, such as SettingsUI.", new RumbleModUI.Tags{ IsSummary = true });
            RumbleModUI.UI.instance.AddMod(sliderOptions);
        }

        private static void SliderChanged(object sender, EventArgs args)
        {
            settings[(RumbleModUI.ModSetting)sender].Active = ((RumbleModUI.ValueChange<bool>)args).Value;
        }

        private static IEnumerator LateUpdateSlider(float value)
        {
            yield return null;
            UpdateSlider(value);
        }

        private static void UpdateSlider(float value)
        {
            if (slider != null && activeOption != null)
            {
                Setting setting = settings[activeOption];
                InteractionSlider interactionSlider = slider.GetComponentInChildren<InteractionSlider>();
                interactionSlider.MoveToValue(interactionSlider.ConvertToValue(Math.Min(Math.Max((value - setting.Min) / (setting.Max - setting.Min), 0.0f), 1.0f)));
            }
        }

        // ----------------- API -----------------

        // Add a setting to the slider. Players will be able to select your setting from the F10 menu. This should be done before the Gym loads.
        // The returned object contains events that trigger when the value or active state of the setting changes along with the current value and active state.
        public static Setting AddSetting(string name, string description, float minValue, float maxValue, float defaultValue, int decimalPlaces = 0)
        {
            RumbleModUI.ModSetting<bool> option = sliderOptions.AddToList(name, false, 1, description, new RumbleModUI.Tags());
            option.SavedValueChanged += SliderChanged;

            float min = Math.Min(minValue, maxValue);
            float max = Math.Max(minValue, maxValue);
            decimalPlaces = Math.Max(decimalPlaces, 0);
            float value = Math.Min(Math.Max(defaultValue, min), max);
            bool active = false;
            Setting setting = new Setting(min, max, decimalPlaces, value, active);
            settings.Add(option, setting);
            settingsReversed.Add(setting, option);

            if (sliderOptions.GetUIStatus())
                RumbleModUI.UI.instance.ForceRefresh();

            return setting;
        }

        // Remove a setting from the slider.
        /* DISABLED AS REMOVING SETTINGS IS NOT SUPPORTED BY RumbleModUI
        public static void RemoveSetting(Setting setting)
        {
            if (settingsDataReversed.ContainsKey(setting))
            {
                RumbleModUI.ModSetting option = settingsDataReversed[setting];
                option.SavedValueChanged -= SliderChanged;
                settingsData.Remove(option);
                settingsDataReversed.Remove(setting);
                // TODO: Delete the option from the UI

                if (sliderOptions.GetUIStatus())
                    RumbleModUI.UI.instance.ForceRefresh();
            }
        }*/
    }
}
