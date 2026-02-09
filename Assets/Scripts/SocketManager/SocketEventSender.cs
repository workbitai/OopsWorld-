using UnityEngine;

namespace NewGame.Socket
{
    public class SocketEventSender : MonoBehaviour
    {
        public static SocketEventSender Instance;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Common method to send ANY socket event with ANY data
        /// </summary>
        public void SendEvent(string eventName, object payload = null)
        {
            if (SocketConnection.Instance == null)
            {
                Debug.LogError("SocketConnection instance not found");
                return;
            }

            if (SocketConnection.Instance.CurrentState != SocketState.Connected)
            {
                Debug.LogWarning(
                    $"Cannot send event [{eventName}] | Socket State = {SocketConnection.Instance.CurrentState}"
                );
                return;
            }

            SocketConnection.Instance.Send(eventName, payload);

            Debug.Log(
                $"<color=#00e5ff><b>COMMON SEND</b></color> → " +
                $"<color=yellow>{eventName}</color>"
            );
        }





        #region EVENT SEND

        public static void SendJoinGame(string userId, int maxPlayers, string gameLobbyId)
        {
            if (SocketConnection.Instance == null)
            {
                Debug.LogError("SocketConnection instance not found");
                return;
            }

            if (SocketConnection.Instance.CurrentState != SocketState.Connected)
            {
                Debug.LogWarning(
                    $"joinGame not sent | Socket State = {SocketConnection.Instance.CurrentState}"
                );
                return;
            }

            var payload = new
            {
                user_id = userId,
                maxPlayers = maxPlayers,
                gamelobby_id = gameLobbyId
            };

            SocketConnection.Instance.SendWithAck("joinGame", payload);

            Debug.Log(
                $"<color=#00ffcc><b>SEND → joinGame</b></color> " +
                $"user_id={userId}, maxPlayers={maxPlayers}, gamelobby_id={gameLobbyId}"
            );
        }


        #endregion
    }
}
