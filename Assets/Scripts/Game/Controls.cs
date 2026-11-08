using Arc;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// All player input through Unity's Input System: keyboard + mouse AND gamepad (Xbox / PlayStation) at the same time.
    ///
    ///                     Klavye / Fare          Oyun kolu
    ///   Yürü / Yönlendir   W A S D               Sol çubuk
    ///   Kamera             Fare                  Sağ çubuk
    ///   Gaz / Fren         W / S                 Sağ tetik (RT) / Sol tetik (LT)
    ///   Koş                Sol Shift             Sol çubuğa bas (L3)
    ///   El freni           Boşluk                A / Çarpı
    ///   Bin / İn / Gir     E                     Y / Üçgen
    ///   Korna              H                     X / Kare
    ///   Kamerayı arkaya al C                     Sağ çubuğa bas (R3)
    ///   Sokağa geri dön    R                     Select / Share
    /// </summary>
    public class Controls
    {
        readonly InputAction move, look, throttle, brake, sprint, handbrake, interact, horn, recenter, reset;

        public Controls()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta").WithProcessor("scaleVector2(x=0.08,y=0.08)");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=2.4,y=1.6)");

            throttle = new InputAction("Throttle", InputActionType.Value, "<Gamepad>/rightTrigger");
            brake = new InputAction("Brake", InputActionType.Value, "<Gamepad>/leftTrigger");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            handbrake = new InputAction("Handbrake", InputActionType.Button);
            handbrake.AddBinding("<Keyboard>/space");
            handbrake.AddBinding("<Gamepad>/buttonSouth");

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Keyboard>/f");
            interact.AddBinding("<Gamepad>/buttonNorth");

            horn = new InputAction("Horn", InputActionType.Button);
            horn.AddBinding("<Keyboard>/h");
            horn.AddBinding("<Gamepad>/buttonWest");

            recenter = new InputAction("Recenter", InputActionType.Button);
            recenter.AddBinding("<Keyboard>/c");
            recenter.AddBinding("<Gamepad>/rightStickPress");

            reset = new InputAction("Reset", InputActionType.Button);
            reset.AddBinding("<Keyboard>/r");
            reset.AddBinding("<Gamepad>/select");

            foreach (var a in new[] { move, look, throttle, brake, sprint, handbrake, interact, horn, recenter, reset }) a.Enable();
        }

        /// <summary>Walking direction / bike steering (x) and keyboard throttle (y). -1..1.</summary>
        public Vector2 Move
        {
            get
            {
                var v = move.ReadValue<Vector2>();
                if (Inputs.SimMove != Vector2.zero) v = Inputs.SimMove;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Camera rotation this frame (degrees-ish).</summary>
        public Vector2 Look => look.ReadValue<Vector2>();

        /// <summary>True if the last camera input came from a mouse (no time scaling needed).</summary>
        public bool LookIsMouse => look.activeControl != null && look.activeControl.device is Mouse;

        /// <summary>Bike throttle 0..1 (W or right trigger).</summary>
        public float Throttle => Mathf.Clamp01(Mathf.Max(throttle.ReadValue<float>(), Move.y));

        /// <summary>Bike brake / reverse 0..1 (S or left trigger).</summary>
        public float Brake => Mathf.Clamp01(Mathf.Max(brake.ReadValue<float>(), -Move.y));

        public bool Sprint => sprint.IsPressed();
        public bool Handbrake => handbrake.IsPressed();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool HornPressed => horn.WasPressedThisFrame();
        public bool RecenterPressed => recenter.WasPressedThisFrame();
        public bool ResetPressed => reset.WasPressedThisFrame();

        public static bool UsingGamepad => Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
    }
}
