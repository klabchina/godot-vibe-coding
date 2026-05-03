using Server.Room;

namespace Server.Session;

public static class DisconnectPolicy
{
    public static void KickByConnection(SessionManager sessionManager, RoomManager roomManager, string connectionId)
    {
        if (!sessionManager.TryGetByConnection(connectionId, out var session) || session == null)
        {
            return;
        }

        var roomId = session.RoomId;
        var playerId = session.PlayerId;

        sessionManager.Remove(playerId);

        if (string.IsNullOrEmpty(roomId))
        {
            return;
        }

        roomManager.DestroyRoom(roomId);

        if (!sessionManager.TryGetByRoom(roomId, out var sessions))
        {
            return;
        }

        foreach (var s in sessions)
        {
            s.RoomId = null;
            s.State = SessionState.Idle;
        }
    }
}
