using System.Timers;
namespace BlazorServerChat2.Data
{
    /// <summary>
    /// チャットルームに何人いるか管理するクラス
    /// SingletonでDIすることを想定
    /// </summary>
    public class Room
    {
        private System.Timers.Timer timer;
        public event Action OnChange;
        /// <summary>
        /// 在室管理辞書
        /// </summary>
        public Dictionary<string, DateTime> room = new Dictionary<string, DateTime>();
        /// <summary>
        /// 在室人数
        /// </summary>
        public int roomCount = 0;

        /// <summary>
        /// コンストラクタ
        /// タイマーを設定
        /// </summary>
        public Room()
        {
            timer = new System.Timers.Timer(10000);
            timer.Elapsed += (sender, e) =>
            {
                CheckTime();
                roomCount = room.Count;
                NotifyStateChanged();
            };
            timer.Start();
        }

        /// <summary>
        /// 入室したときの処理
        /// 在室管理辞書に追加して在室人数を更新する
        /// </summary>
        /// <param name="userid">入室したユーザID</param>
        public void SendMsg(string userid)
        {
            if (room.ContainsKey(userid))
            {
                room.Remove(userid);
                room.Add(userid, DateTime.Now);
            }
            else
            {
                room.Add(userid, DateTime.Now);
            }
            roomCount = room.Count;
            NotifyStateChanged();
        }

        /// <summary>
        /// 退室したときの処理
        /// 在室管理辞書から削除して在室人数を更新する
        /// </summary>
        /// <param name="userid">退室したユーザID</param>
        public void LeaveRoom(string userid)
        {
            if (room.ContainsKey((userid)))
            {
                room.Remove((userid));
            }
            roomCount = room.Count;
            NotifyStateChanged();
        }

        /// <summary>
        /// タイマー判定
        /// 1時間発言がなければ退室したものとみなす
        /// </summary>
        private void CheckTime()
        {
            var DeleteObj = room.Where(x => x.Value <= DateTime.Now.AddHours(-1)).Select(x => x.Key);
            foreach (var x in DeleteObj)
            {
                room.Remove(x);
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

    }
}
