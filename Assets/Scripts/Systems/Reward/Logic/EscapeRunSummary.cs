using System;
using System.Collections.Generic;
using CurtainCall.Results;

namespace CurtainCall.Reward
{
    /// <summary>Current generation totals, independent of the ten-result UI history and ledger pages.</summary>
    [Serializable]
    public sealed class EscapeRunStatistics
    {
        [Serializable] public sealed class Row
        {
            public string trick;
            public int difficulty, attempts, clears;
            public long cheer;
        }
        public List<Row> rows = new();
        public long teamBest, personalBest, earned, spent;
        public int personalModel = -1;
        public bool hasPersonal;
        public ulong personalClient;
        public int generation;
        string lastResult;
        int ledgerRead;

        public void Reset(int nextGeneration, int ledgerCount)
        {
            rows.Clear(); teamBest = personalBest = earned = spent = 0;
            personalModel = -1; personalClient = 0; hasPersonal = false;
            generation = nextGeneration; lastResult = null; ledgerRead = ledgerCount;
        }

        public bool Record(PerformanceResult result, int difficulty, Func<ulong, int> modelFor)
        {
            if (result == null || result.Performance.Generation != generation || result.Performance.Id == lastResult) return false;
            if (difficulty < 1 || difficulty > 3) throw new ArgumentOutOfRangeException(nameof(difficulty));
            lastResult = result.Performance.Id;
            var row = rows.Find(r => r.trick == result.Performance.TrickId && r.difficulty == difficulty);
            if (row == null) { row = new Row { trick = result.Performance.TrickId, difficulty = difficulty }; rows.Add(row); }
            row.attempts++;
            if (result.Succeeded) row.clears++;
            long team = 0;
            foreach (var person in result.Participants)
            {
                team = checked(team + person.Segment);
                if (person.Segment > personalBest || !hasPersonal && person.Connected)
                {
                    hasPersonal = true; personalBest = person.Segment; personalClient = person.ClientId;
                    personalModel = modelFor?.Invoke(person.ClientId) ?? -1;
                }
            }
            row.cheer = checked(row.cheer + team);
            teamBest = Math.Max(teamBest, team);
            return true;
        }

        public void ReadLedger(IReadOnlyList<FundEntry> ledger)
        {
            for (; ledgerRead < ledger.Count; ledgerRead++)
            {
                var entry = ledger[ledgerRead];
                if (entry.Generation != generation) continue;
                if (entry.Kind == FundEntryKind.PerformanceIncome) earned = checked(earned + entry.Amount);
                else if (entry.Kind == FundEntryKind.Expense) spent = checked(spent - entry.Amount);
            }
        }

        public void Validate(int expectedGeneration)
        {
            if (generation != expectedGeneration || rows == null || rows.Count > 32 || teamBest < 0 || personalBest < 0
                || earned < 0 || spent < 0 || personalModel < -1 || personalModel > 63) throw new ArgumentException("Invalid ending statistics");
            var keys = new HashSet<string>();
            foreach (var row in rows)
                if (row == null || string.IsNullOrEmpty(row.trick) || row.trick.Length > 100 || row.difficulty < 1 || row.difficulty > 3
                    || row.attempts < 1 || row.clears < 0 || row.clears > row.attempts || row.cheer < 0
                    || !keys.Add(row.trick + ":" + row.difficulty)) throw new ArgumentException("Invalid ending statistics row");
        }
    }
}
