namespace Game.Ecs.Components;

public enum GameMode
{
    SinglePlayer = 0,
    MultiPlayer = 1,
}

public class GameSettingComponent
{
    public GameMode Mode = GameMode.SinglePlayer;
}
