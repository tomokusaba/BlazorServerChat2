using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BlazorServerChat2.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<IconMaster> IconMaster { get; set; }
        public DbSet<UserChatSetting> UserChatSetting { get; set; }
        public DbSet<NetaMastar> NetaMastar { get; set; }
        public DbSet<MenuMaster> MenuMaster { get; set; }
        public DbSet<Osaifu> Osaifus { get; set; }
    }

    /// <summary>
    /// チャットの履歴
    /// </summary>
    public class Chat
    {
        /// <summary>
        /// 発言日時
        /// </summary>
        [Key]
        public DateTime Time { get; set; } = DateTime.Now;
        /// <summary>
        /// 発言者
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 発言内容
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// 発言者ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        

    }

    /// <summary>
    /// アイコンのデータ
    /// </summary>
    public class IconMaster
    {
        /// <summary>
        /// アイコン登録連番
        /// </summary>
        [Key]
        public int IconNumber { get; set; }
        /// <summary>
        /// アイコンデータ
        /// </summary>
        public byte[]? Icon { get; set; }
        /// <summary>
        /// アイコンファイル名
        /// </summary>
        public string IconName { get; set; } = string.Empty;
    }

    /// <summary>
    /// ユーザごとのアイコン・チャット表示色の設定
    /// </summary>
    public class UserChatSetting
    {
        /// <summary>
        /// ユーザID
        /// </summary>
        [Key]
        public string? Id { get; set; }
        /// <summary>
        /// アイコン登録連番
        /// </summary>
        public int IconNumber { get; set; }
        /// <summary>
        /// チャット表示色 rgb(x,y,z) 形式
        /// </summary>
        public string BackGroundColor { get; set; } = string.Empty;
    }

    /// <summary>
    /// ネタ帳
    /// </summary>
    public class NetaMastar
    {
        /// <summary>
        /// ネタ登録連番
        /// </summary>
        [Key]
        public int NetaId { get; set;}
        /// <summary>
        /// ネタ本体
        /// </summary>
        public string? Neta { get; set; }
        /// <summary>
        /// ネタ登録日時
        /// </summary>
        public DateTime CreateDate { get; set; } = DateTime.Now;
    }

    public class MenuMaster
    {
        [Key]
        public int MenuId { get; set; }
        public string? MenuName { get; set; }

        public int Price { get; set; }
        public int WaitTime { get; set; }
    }

    public class Osaifu
    {
        [Key]
        public string? Name { get; set; }
        public int Kingaku { get; set; }
    }
}