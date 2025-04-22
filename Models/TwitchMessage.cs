using System;

namespace TwitchDeckOverlay.Models
{
    public class TwitchMessage
    {
        public string Username { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public TwitchMessage(string username, string content)
        {
            Username = username;
            Content = content;
            Timestamp = DateTime.Now;
        }
    }
}