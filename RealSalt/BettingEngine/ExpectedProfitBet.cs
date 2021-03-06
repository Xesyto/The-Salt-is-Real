﻿using System;
using System.Collections.Generic;
using System.Linq;
using ChromiumConsole;
using ChromiumConsole.EventArguments;
using RealSalt.Data;
using Serilog;

namespace RealSalt.BettingEngine
{
    class ExpectedProfitBet : IBettingEngine
    {
        private ForbiddingManse _forbiddingManse;
        private readonly int _reliableMatchCount = 5;
        private bool _exhibitionMatches;

        public ExpectedProfitBet(bool exhibitionMatches = false)
        {
            _forbiddingManse = Program.ForbiddingManse;

            _exhibitionMatches = exhibitionMatches;
        }

        public BettingPlan PlaceBet(MatchStartEventArgs matchArgs)
        {
            var betSymbol = " ";

            var betSalt = 10;
            var betCharacter = SaltyConsole.Players.Unknown;

            var bluePlayer = _forbiddingManse.GetOrCreateCharacter(matchArgs.BluePlayer);
            var redPlayer = _forbiddingManse.GetOrCreateCharacter(matchArgs.RedPlayer);

            var (bluePlayerProfit, bluePlayerMatchcount) = GetProfitAndMatchcount(bluePlayer);
            var (redPlayerProfit, redPlayerMatchcount) = GetProfitAndMatchcount(redPlayer);

            if (redPlayerProfit.Equals(bluePlayerProfit))
            {
                //Either identical profit expectations or more likely two characters without recorded matches
                return new BettingPlan
                {
                    Symbol = betSymbol,
                    Character = SaltyConsole.Players.Unknown,
                    Salt = betSalt
                };
            }

            if (bluePlayerProfit != 0 && redPlayerProfit != 0)
            {
                betSymbol = "=";
            }
            else if (bluePlayerProfit != 0 || redPlayerProfit != 0)
            {
                betSymbol = "-";
            }
            
            if (redPlayerProfit > bluePlayerProfit)
            {
                betCharacter = SaltyConsole.Players.RedPlayer;
                betSalt = BaseBetAmount(redPlayerProfit, matchArgs.Salt);

                if (redPlayerMatchcount > _reliableMatchCount && bluePlayerMatchcount > _reliableMatchCount)
                {
                    betSalt += AdditionalBetAmount(redPlayerProfit, betSalt);
                }
                
                Log.Verbose("Better - Profit: {RedPlayer} odds {RedPlayerCertainty}{RedPlayerProfit:N4} >>> {Blueplayer} odds {BluePlayerCertainty}{BluePlayerProfit:N4}.",
                    redPlayer.Name,
                    redPlayerMatchcount <= _reliableMatchCount ? "~" : "",
                    redPlayerProfit,
                    bluePlayer.Name,
                    bluePlayerMatchcount <= _reliableMatchCount ? "~" : "",
                    bluePlayerProfit);
            }
            else
            {
                betCharacter = SaltyConsole.Players.BluePlayer;
                betSalt = BaseBetAmount(bluePlayerProfit, matchArgs.Salt);

                if (redPlayerMatchcount > _reliableMatchCount && bluePlayerMatchcount > _reliableMatchCount)
                {
                    betSalt += AdditionalBetAmount(bluePlayerProfit, betSalt);
                }
                
                Log.Verbose("Better - Profit: {RedPlayer} odds {RedPlayerCertainty}{RedPlayerProfit:N4} <<< {Blueplayer} odds {BluePlayerCertainty}{BluePlayerProfit:N4}.",
                    redPlayer.Name,
                    redPlayerMatchcount <= _reliableMatchCount ? "~" : "",
                    redPlayerProfit,
                    bluePlayer.Name,
                    bluePlayerMatchcount <= _reliableMatchCount ? "~" : "",
                    bluePlayerProfit);
            }

            return new BettingPlan
            {
                Symbol = betSymbol,
                Character = betCharacter,
                Salt = betSalt
            };
        }

        private (double,int) GetProfitAndMatchcount(Character character)
        {
            int losses = GetNumberOfLosses(character);
            double expectedProfit = losses * -1;


            var winningMatches = GetWinningMatches(character);

            foreach (var winningMatch in winningMatches)
            {
                expectedProfit += GetMatchWinOdds(winningMatch);
            }

            var totalMatches = losses + winningMatches.Count;

            expectedProfit = expectedProfit / totalMatches;

            return (expectedProfit, totalMatches);
        }

        public double GetMatchWinOdds(MatchRecord match)
        {
            double winnerSalt = match.WinnerSalt;
            double loserSalt = match.LoserSalt;

            return winnerSalt / loserSalt;
        }

        public List<MatchRecord> GetWinningMatches(Character character)
        {
            if (_exhibitionMatches)
            {
                return _forbiddingManse.Matches.Where(c =>
                    c.MatchType == MatchType.Exhibition 
                    && c.WinnerCharacterId == character.CharacterId)
                    .ToList();
            }

            return _forbiddingManse.Matches.Where(c =>
                (c.MatchType == MatchType.MatchMaking || c.MatchType == MatchType.Tournament)
                && c.WinnerCharacterId == character.CharacterId)
                .ToList();
        }

        public int GetNumberOfLosses(Character character)
        {
            if (_exhibitionMatches)
            {
                return _forbiddingManse.Matches.Count(c =>
                    c.MatchType == MatchType.Exhibition && c.LoserCharacterId == character.CharacterId);
            }
            
            return _forbiddingManse.Matches.Count(c =>
                    (c.MatchType == MatchType.MatchMaking || c.MatchType == MatchType.Tournament) 
                    && c.LoserCharacterId == character.CharacterId);
            
        }

        private int BaseBetAmount(double expectedProfit, int salt)
        {
            var digits = Math.Floor(Math.Log10(salt) + 1);
            var targetDigits = (int)digits - 3;

            double betAmount = (int)Math.Pow(10, targetDigits);

            if (expectedProfit < 0)
            {
                //Unlikely scenario that both have terrible returns but we're better on the least bad
                betAmount = betAmount * expectedProfit;
            }

            //Don't bet negative amounts.
            if (betAmount < 0)
            {
                betAmount = betAmount * -1;
            }

            return (int) betAmount;
        }

        private int AdditionalBetAmount(double expectedProfit, int startingAmount)
        {
            //Don't bet more if we didn't pass twice expected profit
            if (expectedProfit < 2)
            {
                return 0;
            }

            //adjust by one since we are adding to the base bet not modifying the base bet.
            expectedProfit -= 1;

            //map our linear expected profit to a log multiplier.
            var scaledMultiplier = Scale(expectedProfit);

            return (int)(startingAmount * scaledMultiplier);
        }

        private double Scale(double value, double maxInputValue = 100, double maxOutputValue = 4)
        {
            if (value <= 1)
            {
                return 0; // log is undefined for 0, log(1) = 0
            }

            if (value > maxInputValue)
            {
                value = maxInputValue;
            }
                            
            return maxOutputValue * Math.Log(value) / Math.Log(maxInputValue);
        }
    }
}
