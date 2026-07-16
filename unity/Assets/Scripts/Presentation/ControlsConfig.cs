using System.Collections.Generic;
using UnityEngine;

namespace D1U.Presentation
{
    public enum GameAction
    {
        Forward, Reverse, SlideLeft, SlideRight, SlideUp, SlideDown,
        BankLeft, BankRight, FirePrimary, FireSecondary, Flare, PreferHoming, Automap,
    }

    /// <summary>
    /// Rebindable controls + mouse tuning (the kconfig.c control set, PlayerPrefs
    /// persisted). Shared live between the menu page and ShipController, so
    /// changes apply immediately.
    /// </summary>
    public sealed class ControlsConfig
    {
        public static readonly (GameAction Action, string Label, KeyCode Default)[] Bindables =
        {
            (GameAction.Forward,       "Accelerate",          KeyCode.W),
            (GameAction.Reverse,       "Reverse",             KeyCode.S),
            (GameAction.SlideLeft,     "Slide left",          KeyCode.A),
            (GameAction.SlideRight,    "Slide right",         KeyCode.D),
            (GameAction.SlideUp,       "Slide up",            KeyCode.Space),
            (GameAction.SlideDown,     "Slide down",          KeyCode.LeftControl),
            (GameAction.BankLeft,      "Bank left",           KeyCode.Q),
            (GameAction.BankRight,     "Bank right",          KeyCode.E),
            (GameAction.FirePrimary,   "Fire primary",        KeyCode.Mouse0),
            (GameAction.FireSecondary, "Fire secondary",      KeyCode.Mouse1),
            (GameAction.Flare,         "Drop flare",          KeyCode.F),
            (GameAction.PreferHoming,  "Prefer homing (hold)", KeyCode.H),
            (GameAction.Automap,       "Automap",             KeyCode.Tab),
        };

        readonly Dictionary<GameAction, KeyCode> keys = new Dictionary<GameAction, KeyCode>();

        public float MouseSens = 1f;   // 1 = original DOS default rate
        public bool InvertY;           // false = original (push forward = nose down)
        public bool InvertX;

        public ControlsConfig() => Load();

        public KeyCode Get(GameAction action) => keys[action];
        public bool Held(GameAction action) => Input.GetKey(keys[action]);
        public bool Pressed(GameAction action) => Input.GetKeyDown(keys[action]);

        public void Set(GameAction action, KeyCode key)
        {
            keys[action] = key;
            PlayerPrefs.SetInt("d1u_key_" + action, (int)key);
        }

        public void Load()
        {
            foreach (var (action, _, def) in Bindables)
                keys[action] = (KeyCode)PlayerPrefs.GetInt("d1u_key_" + action, (int)def);
            MouseSens = PlayerPrefs.GetFloat("d1u_mouse_sens", 1f);
            InvertY = PlayerPrefs.GetInt("d1u_invert_y", 0) != 0;
            InvertX = PlayerPrefs.GetInt("d1u_invert_x", 0) != 0;
        }

        public void SaveMouse()
        {
            PlayerPrefs.SetFloat("d1u_mouse_sens", MouseSens);
            PlayerPrefs.SetInt("d1u_invert_y", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("d1u_invert_x", InvertX ? 1 : 0);
        }

        public void ResetDefaults()
        {
            foreach (var (action, _, def) in Bindables)
                Set(action, def);
            MouseSens = 1f;
            InvertY = false;
            InvertX = false;
            SaveMouse();
        }

        public static string KeyName(KeyCode key) => key switch
        {
            KeyCode.Mouse0 => "LMB",
            KeyCode.Mouse1 => "RMB",
            KeyCode.Mouse2 => "MMB",
            KeyCode.LeftControl => "L-CTRL",
            KeyCode.RightControl => "R-CTRL",
            KeyCode.LeftShift => "L-SHIFT",
            KeyCode.RightShift => "R-SHIFT",
            _ => key.ToString().ToUpperInvariant(),
        };
    }
}
