/// <summary>
/// One value: how hard and which way something is steering.
/// Swappable: touch today, keyboard/gamepad/AI-driven later, without touching Player.
/// </summary>
public interface IInputReader
{
    float TurnInput { get; }
}