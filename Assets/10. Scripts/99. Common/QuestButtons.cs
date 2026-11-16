using UnityEngine.InputSystem;

namespace StudyXR.Common
{
    public static class QuestButtons
    {
        public static InputAction Create(
            QuestButton button,
            string actionName,
            string keyboardFallback = null)
        {
            var action = new InputAction(actionName, InputActionType.Button, Path(button));

            if (string.IsNullOrEmpty(keyboardFallback) == false) action.AddBinding(keyboardFallback);
            return action;
        }

        public static string Path(QuestButton button)
        {
            switch (button)
            {
                case QuestButton.A: return "<XRController>{RightHand}/{PrimaryButton}";
                case QuestButton.B: return "<XRController>{RightHand}/{SecondaryButton}";
                case QuestButton.X: return "<XRController>{LeftHand}/{PrimaryButton}";
                case QuestButton.Y: return "<XRController>{LeftHand}/{SecondaryButton}";
                case QuestButton.RightStickClick: return "<XRController>{RightHand}/{Primary2DAxisClick}";
                default: return "<XRController>{LeftHand}/{Primary2DAxisClick}";
            }
        }

        public static string Label(QuestButton button)
        {
            switch (button)
            {
                case QuestButton.A: return "오른손 A";
                case QuestButton.B: return "오른손 B";
                case QuestButton.X: return "왼손 X";
                case QuestButton.Y: return "왼손 Y";
                case QuestButton.RightStickClick: return "오른손 스틱 누르기";
                default: return "왼손 스틱 누르기";
            }
        }

        public const string Trigger = "트리거";
        public const string Grip = "그립";
        public const string Stick = "스틱";
        public const string Menu = "왼손 메뉴(≡)";
    }
}
