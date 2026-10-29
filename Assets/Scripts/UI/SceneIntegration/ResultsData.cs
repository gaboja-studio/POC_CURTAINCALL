using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public enum ResultsOutcome { Failure, Success, GrandSuccess }

    /// <summary>Copied display values. Progress, distance and identity are supplied by the project.</summary>
    public sealed class ResultsMemberPresentation
    {
        public int Id { get; private set; }
        public string Label { get; private set; }
        public float? NormalizedProgress { get; private set; }
        public float? NormalizedFall { get; private set; }
        public string DistanceLabel { get; private set; }
        public Color Color { get; private set; }
        public bool IsLocalPlayer { get; private set; }

        public ResultsMemberPresentation(int id, string label, float? normalizedProgress,
            string distanceLabel, Color color, bool isLocalPlayer, float? normalizedFall = null)
        {
            if (id < 1 || id > 4 || string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("A result member needs a unique participant ID in 1–4 and a non-empty label.");
            if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) ||
                color.r < 0 || color.r > 1 || color.g < 0 || color.g > 1 || color.b < 0 || color.b > 1)
                throw new ArgumentException("Member colors must contain finite RGB channels in [0,1].");
            Id = id; Label = label; NormalizedProgress = normalizedProgress;
            NormalizedFall = normalizedFall; DistanceLabel = distanceLabel ?? "";
            Color = new Color(color.r, color.g, color.b, 1); IsLocalPlayer = isLocalPlayer;
        }

        internal ResultsMemberPresentation Copy()
        { return new ResultsMemberPresentation(Id, Label, NormalizedProgress, DistanceLabel, Color, IsLocalPlayer, NormalizedFall); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    /// <summary>Read-only result snapshot; this class does not calculate scores, rewards or funds.</summary>
    public sealed class ResultsPresentation
    {
        public string SessionId { get; private set; }
        public string Revision { get; private set; }
        public ResultsOutcome Outcome { get; private set; }
        public string Title { get; private set; }
        public string Context { get; private set; }
        public string Subtitle { get; private set; }
        public string ProgressHeading { get; private set; }
        public string CourseLabel { get; private set; }
        public float? NormalizedMaximum { get; private set; }
        public string BaseLabel { get; private set; }
        public string BaseAmount { get; private set; }
        public string BonusLabel { get; private set; }
        public string BonusAmount { get; private set; }
        public string FundsLabel { get; private set; }
        public string ContributionText { get; private set; }
        public string FundsAmount { get; private set; }
        public string Footer { get; private set; }
        public string AcknowledgeLabel { get; private set; }
        public bool AcknowledgeEnabled { get; private set; }
        private readonly ReadOnlyCollection<ResultsMemberPresentation> members;
        public IReadOnlyList<ResultsMemberPresentation> Members { get { return members; } }

        public ResultsPresentation(string sessionId, string revision, ResultsOutcome outcome, string title,
            string context, string subtitle, string progressHeading, string courseLabel,
            float? normalizedMaximum, string baseLabel, string baseAmount, string bonusLabel, string bonusAmount,
            string fundsLabel, string contributionText, string fundsAmount, string footer, string acknowledgeLabel,
            bool acknowledgeEnabled, params ResultsMemberPresentation[] memberDefinitions)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A result needs a session ID, revision and title.");
            if (!Enum.IsDefined(typeof(ResultsOutcome), outcome)) throw new ArgumentOutOfRangeException("outcome");
            if (memberDefinitions == null || memberDefinitions.Length < 1 || memberDefinitions.Length > 4)
                throw new ArgumentException("This result view displays one to four members.");
            var copied = new ResultsMemberPresentation[memberDefinitions.Length];
            var ids = new HashSet<int>();
            int localCount = 0;
            for (int i = 0; i < copied.Length; i++)
            {
                var member = memberDefinitions[i];
                if (member == null || !ids.Add(member.Id)) throw new ArgumentException("Result member IDs must be unique.");
                if (member.IsLocalPlayer) localCount++;
                copied[i] = member.Copy();
            }
            if (localCount > 1) throw new ArgumentException("At most one result member can be marked as the local player.");
            members = Array.AsReadOnly(copied); SessionId = sessionId; Revision = revision; Outcome = outcome; Title = title;
            Context = context ?? ""; Subtitle = subtitle ?? ""; ProgressHeading = progressHeading ?? "";
            CourseLabel = courseLabel ?? ""; NormalizedMaximum = normalizedMaximum;
            BaseLabel = baseLabel ?? ""; BaseAmount = baseAmount ?? ""; BonusLabel = bonusLabel ?? ""; BonusAmount = bonusAmount ?? "";
            FundsLabel = fundsLabel ?? ""; ContributionText = contributionText ?? ""; FundsAmount = fundsAmount ?? ""; Footer = footer ?? "";
            AcknowledgeLabel = acknowledgeLabel ?? ""; AcknowledgeEnabled = acknowledgeEnabled;
        }

        internal ResultsPresentation Copy()
        {
            var copied = new ResultsMemberPresentation[members.Count];
            for (int i = 0; i < copied.Length; i++) copied[i] = members[i];
            return new ResultsPresentation(SessionId, Revision, Outcome, Title, Context, Subtitle, ProgressHeading,
                CourseLabel, NormalizedMaximum, BaseLabel, BaseAmount, BonusLabel, BonusAmount, FundsLabel, ContributionText,
                FundsAmount, Footer, AcknowledgeLabel, AcknowledgeEnabled, copied);
        }
    }
}
