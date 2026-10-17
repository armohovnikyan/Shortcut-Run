using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The stack of carried boards sways: the lowest part stays put, the boards above it lean out sideways,
/// more the higher they are. Turning leans the stack away from the turn (it lags behind); running straight
/// lets it wobble side to side. A small stack barely moves. Driven by how fast the runner actually turns,
/// so it works the same for the player and the NPCs. Only moves the boards — no gameplay.
/// Plain class: Runner owns it as a serialized field.
/// </summary>
[Serializable]
public class BoardStackSway
{
    [Tooltip("Sideways offset of the top board at full sway, in metres.")]
    [SerializeField, Min(0f)] private float maxOffset = 0.3f;
    [Tooltip("Boards needed for the full sway; a smaller stack sways proportionally less.")]
    [SerializeField, Min(1)] private int boardsForFullSway = 20;
    [Tooltip("Share of the stack at the bottom that stays put (0.33 = the lowest third).")]
    [SerializeField, Range(0f, 1f)] private float steadyPart = 0.33f;
    [Tooltip("Turning this fast (degrees per second) leans the stack all the way.")]
    [SerializeField, Min(1f)] private float fullLeanTurnRate = 90f;
    [Tooltip("Side-to-side wobble while running straight: how fast (radians per second).")]
    [SerializeField, Min(0f)] private float wobbleSpeed = 4f;
    [Tooltip("Side-to-side wobble while running straight: how big, compared to a full lean.")]
    [SerializeField, Range(0f, 1f)] private float wobbleAmount = 1f;
    [Tooltip("How quickly the boards catch up with where the sway wants them.")]
    [SerializeField, Min(0f)] private float followSharpness = 25f;

    private float lastYaw;
    private bool hasYaw;

    public void Tick(IReadOnlyList<BaseBoard> boards, Transform stackPoint, Transform runner, float spacing, float deltaTime)
    {
        float yaw = runner.eulerAngles.y;
        float turnRate = hasYaw && deltaTime > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / deltaTime : 0f;
        lastYaw = yaw;
        hasYaw = true;

        int count = boards.Count;
        if (count == 0 || stackPoint == null) return;

        // Turning right (yaw grows) leans the stack left, and the other way round; straight = wobble.
        float lean = -Mathf.Clamp(turnRate / fullLeanTurnRate, -1f, 1f);
        float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount * (1f - Mathf.Abs(lean));
        float topOffset = (lean + wobble) * maxOffset * Mathf.Clamp01((float)count / boardsForFullSway);
        Vector3 side = stackPoint.InverseTransformVector(runner.right); // the runner's right, in the stack's space

        int steady = Mathf.FloorToInt(count * steadyPart);
        float follow = 1f - Mathf.Exp(-followSharpness * deltaTime);

        for (int i = 0; i < count; i++)
        {
            BaseBoard board = boards[i];
            if (board == null) continue;

            Vector3 target = Vector3.up * spacing * i;
            if (i >= steady)
            {
                float height = (float)(i - steady + 1) / (count - steady); // 0..1 within the swaying part
                target += side * (topOffset * height * height);
            }
            board.transform.localPosition = i < steady
                ? target
                : Vector3.Lerp(board.transform.localPosition, target, follow);
        }
    }
}
