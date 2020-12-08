﻿namespace Common.DbEF
{
    using System.ComponentModel.DataAnnotations;
    public class Messages
    {
        #region Properties

        [Key]
        public string MessageID { get; set; }
        public int ChatID { get; set; }
        public string From { get; set; }
        public string Text { get; set; }
        public string Time { get; set; }

        #endregion Properties

    }
}
