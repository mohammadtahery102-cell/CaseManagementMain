using System;

namespace CaseManagement.Models
{
    // ساختار ساده متناظر با جدول TblUsers.
    public class UserModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
