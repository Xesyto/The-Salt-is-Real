﻿using System;

namespace RealSalt.Data
{
    public class Character
    {
        public int CharacterId { get; set; }
        public string Name { get; set; }

        public int TotalWins => Program.ForbiddingManse.GetWins(this);
        public int TotalLosses => Program.ForbiddingManse.GetLosses(this);

        public int Matches => TotalWins + TotalLosses;
        public double WinPercent
        {
            get
            {
                var percent=  (double)TotalWins / (double) Matches * 100;
                return Math.Round(percent, 1);
            }
        }

        public bool IsReliableData => Matches > 4;
    }
    
}
