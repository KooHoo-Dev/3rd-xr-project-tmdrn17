using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace StudyXR.Common
{
    public class ZoneHub : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private TeleportationProvider teleportation;
        [SerializeField] private ScreenFade screenFade;

        [Header("구역 (같은 순서)")]
        [SerializeField] private Transform[] arrivals;
        [SerializeField] private string[] titles;
        [SerializeField] private string[] descriptions;
        [SerializeField] private TMP_Text[] boards;

        [Header("이동")]
        [SerializeField] private float fadeSeconds = 0.35f;

        [Header("걸어서 옮긴 경우")]
        [SerializeField] private bool trackByPosition = true;

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool enableTestKeys = true;

        private Transform head;
        private float trackPauseUntil;

        public int Current { get; private set; }
        public int Count => arrivals != null ? arrivals.Length : 0;

        public event Action<int> ZoneChanged;

        private const string MoveHint = "구역 이동 : 아래 버튼을 레이 + " + QuestButtons.Trigger + "로 누르기";

        private void Start()
        {
            Current = 0;
            RefreshBoards();
        }

        private void Update()
        {
            if (enableTestKeys) ReadTestKeys();
            if (trackByPosition) TrackNearestZone();
        }

        private void ReadTestKeys()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null) return;

            if (keyboard.pageDownKey.wasPressedThisFrame) Next();
            if (keyboard.pageUpKey.wasPressedThisFrame) Previous();
        }

        private void TrackNearestZone()
        {
            if (Count < 2 || Time.time < trackPauseUntil) return;
            if (screenFade != null && screenFade.IsFading) return;
            if (head == null)
            {
                Camera cam = Camera.main;

                if (cam == null) return;
                head = cam.transform;
            }

            int nearest = Current;
            float best = float.MaxValue;
            for (int i = 0; i < Count; ++i)
            {
                if (arrivals[i] == null) continue;
                Vector3 d = arrivals[i].position - head.position;
                float sq = d.x * d.x + d.z * d.z;

                if (sq < best) { best = sq; nearest = i; }
            }
            if (nearest != Current) SetCurrent(nearest);
        }

        public void Next()
        {
            if (Count == 0) return;
            GoTo((Current + 1) % Count);
        }

        public void Previous()
        {
            if (Count == 0) return;
            GoTo((Current - 1 + Count) % Count);
        }

        public void GoTo(int index)
        {
            if (index < 0 || index >= Count || arrivals[index] == null) return;
            if (screenFade != null && screenFade.IsFading) return;

            if (screenFade == null || fadeSeconds <= 0f) Teleport(index);
            else screenFade.FadeOutIn(() => Teleport(index), fadeSeconds);
        }

        private void Teleport(int index)
        {
            Transform destination = arrivals[index];

            if (teleportation != null)
            {
                var request = new TeleportRequest
                {
                    destinationPosition = destination.position,
                    destinationRotation = destination.rotation,
                    matchOrientation = MatchOrientation.TargetUpAndForward,
                    requestTime = Time.time,
                };
                teleportation.QueueTeleportRequest(request);
            }

            trackPauseUntil = Time.time + 0.5f;
            SetCurrent(index);
        }

        private void SetCurrent(int index)
        {
            Current = index;
            RefreshBoards();
            ZoneChanged?.Invoke(index);
        }

        private void RefreshBoards()
        {
            if (boards == null) return;
            for (int i = 0; i < boards.Length; ++i)
            {
                if (boards[i] == null) continue;
                var text = new System.Text.StringBuilder();
                text.Append($"<b>구역 {i + 1} / {Count} · {Title(i)}</b>\n");
                if (descriptions != null && i < descriptions.Length) text.Append(descriptions[i]).Append('\n');
                text.Append("<size=90%>");
                for (int k = 0; k < Count; ++k)
                {
                    string mark = k == Current ? "▶ " : "   ";
                    text.Append(mark).Append(k + 1).Append(". ").Append(Title(k)).Append(k == Current ? "  (지금 여기)" : "").Append('\n');
                }
                text.Append("</size>");
                text.Append("<size=90%><color=#9FD0FF>").Append(MoveHint).Append("</color></size>");
                boards[i].text = text.ToString();
            }
        }

        private string Title(int index)
        {
            return titles != null && index < titles.Length ? titles[index] : $"구역 {index + 1}";
        }
    }
}
