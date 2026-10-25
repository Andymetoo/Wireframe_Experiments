using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TestAnxiety
{
    public enum ExamStage { Answering, Scanning, ResultsReady, Reviewing, AwaitingExit, Complete }

    /// <summary>The machine's returned record, with the submitted pencil mark retained separately.</summary>
    public sealed class ExamResultRow
    {
        public ExamQuestion Question { get; private set; }
        public string Prompt { get; private set; }
        public int OriginalAnswer { get; private set; }
        public int ReturnedAnswer { get; private set; }
        public int CorrectAnswer { get; private set; }
        public bool IsCorrect { get; private set; }
        public bool WasChanged { get { return OriginalAnswer != ReturnedAnswer; } }
        public string Note { get; private set; }

        internal ExamResultRow(ExamQuestion question, string prompt, int submittedAnswer)
        {
            Question = question;
            Prompt = prompt;
            OriginalAnswer = submittedAnswer;
            ReturnedAnswer = question.Anomaly == ExamAnomaly.ChangedAnswer ? (submittedAnswer + 1) % 4 : submittedAnswer;
            CorrectAnswer = question.CorrectOption;
            // The alteration is always red-marked, even if the machine has selected the expected option.
            IsCorrect = question.Anomaly != ExamAnomaly.ChangedAnswer && ReturnedAnswer == CorrectAnswer;
            Note = question.ResponseNotes.Count == 4 ? question.ResponseNotes[submittedAnswer] : question.ExaminerNote;
        }
    }

    public sealed class ExamResult
    {
        public int SectionIndex { get; private set; }
        public string Title { get; private set; }
        public IReadOnlyList<ExamResultRow> Rows { get; private set; }
        public int CorrectCount { get; private set; }

        internal ExamResult(int sectionIndex, ExamSection section, int[] submittedAnswers, string[] submittedPrompts)
        {
            SectionIndex = sectionIndex;
            Title = section.Title;
            var rows = new List<ExamResultRow>();
            for (int i = 0; i < section.Questions.Count; i++)
            {
                var row = new ExamResultRow(section.Questions[i], submittedPrompts[i], submittedAnswers[i]);
                rows.Add(row);
                if (row.IsCorrect) CorrectCount++;
            }
            Rows = rows.AsReadOnly();
        }
    }

    /// <summary>
    /// Pure examination state machine. No wall-clock transitions, input devices or Unity objects.
    /// Animation signals completion explicitly; scores never prevent progression.
    /// </summary>
    public sealed class ExamSession
    {
        readonly List<ExamSection> sections = new List<ExamSection>();
        readonly List<ExamResult> history = new List<ExamResult>();
        readonly int[][] answers;
        readonly HashSet<string> initialShirtReads = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> changedShirtReads = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> rearObservations = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> changedShirts = new HashSet<string>(StringComparer.Ordinal);
        int[] pendingAnswers;
        string[] pendingPrompts;
        float quietElapsed;

        public const float QuietIntervalSeconds = 18f;

        public ExamStage Stage { get; private set; }
        public int SectionIndex { get; private set; }
        public int SectionCount { get { return sections.Count; } }
        public ExamSection CurrentSection { get { return sections[SectionIndex]; } }
        public IReadOnlyList<ExamSection> Sections { get; private set; }
        public IReadOnlyList<ExamResult> History { get; private set; }
        public ExamResult LatestResult { get { return history.Count == 0 ? null : history[history.Count - 1]; } }
        public bool BookOpen { get; private set; }
        public int SelectionEdits { get; private set; }
        public bool ArchiveRead { get; private set; }
        public bool ArchiveCollected { get; private set; }
        public bool NeedsArchiveRecord { get { return Stage == ExamStage.Answering && CurrentSection.RequiresArchive && !ArchiveCollected; } }
        public float QuietElapsed { get { return quietElapsed; } }
        public float QuietProgress { get { return quietElapsed / QuietIntervalSeconds; } }
        public bool NeedsQuietInterval { get { return Stage == ExamStage.Scanning && CurrentSection.RequiresQuiet && quietElapsed < QuietIntervalSeconds; } }
        public bool CanFinishScan { get { return Stage == ExamStage.Scanning && !NeedsQuietInterval; } }
        public int FinalReleaseChoice { get { return GetRecordedAnswer("release-choice"); } }
        public bool ShirtInitialRead { get { return initialShirtReads.Count > 0; } }
        public bool RearObserved { get { return rearObservations.Count > 0; } }
        public bool ShirtChanged { get { return changedShirts.Count > 0; } }
        public bool ShirtChangedRead { get { return changedShirtReads.Count > 0; } }
        public bool ShirtPresent { get { return ShirtChanged; } }
        /// <summary>Current-section guidance; unlike the historical flags this supports repeated authored scenarios.</summary>
        public bool NeedsRearObservation
        {
            get
            {
                if (Stage != ExamStage.Answering) return false;
                foreach (ExamQuestion question in CurrentSection.Questions)
                    if (question.Anomaly == ExamAnomaly.Shirt && initialShirtReads.Contains(question.Id) && !rearObservations.Contains(question.Id)) return true;
                return false;
            }
        }
        public bool NeedsShirtRevisit
        {
            get
            {
                if (Stage != ExamStage.Answering) return false;
                foreach (ExamQuestion question in CurrentSection.Questions)
                    if (question.Anomaly == ExamAnomaly.Shirt && rearObservations.Contains(question.Id) && !changedShirtReads.Contains(question.Id)) return true;
                return false;
            }
        }
        public int AnsweredCount
        {
            get
            {
                int count = 0;
                foreach (int answer in answers[SectionIndex]) if (answer >= 0) count++;
                return count;
            }
        }

        /// <summary>Readiness of the sheet. The booklet must additionally be closed before insertion.</summary>
        public bool CanSubmit { get { return SubmissionBlockReason.Length == 0; } }
        public string SubmissionBlockReason
        {
            get
            {
                if (Stage != ExamStage.Answering) return "The machine already has this section.";
                if (NeedsArchiveRecord) return "Retrieve the retained copy in Records, through the classroom entrance.";
                foreach (ExamQuestion question in CurrentSection.Questions)
                {
                    if (question.Anomaly != ExamAnomaly.Shirt) continue;
                    if (!initialShirtReads.Contains(question.Id)) return "Read every question in the booklet before submitting.";
                    if (!rearObservations.Contains(question.Id)) return "Close the booklet and look behind your examination desk.";
                    if (!changedShirts.Contains(question.Id) || !changedShirtReads.Contains(question.Id))
                        return "Return to the booklet and read the shirt question again.";
                }
                if (AnsweredCount != CurrentSection.Questions.Count) return "Fill one bubble for every question in this section.";
                return "";
            }
        }

        public ExamSession(ExamCatalog catalog)
        {
            if (catalog == null || catalog.sections == null || catalog.sections.Length == 0)
                throw new ArgumentException("The examination needs at least one section.", "catalog");
            var sectionIds = new HashSet<string>(StringComparer.Ordinal);
            var questionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SectionDefinition definition in catalog.sections)
            {
                var section = new ExamSection(definition);
                if (!sectionIds.Add(section.Id)) throw new ArgumentException("Duplicate section id: " + section.Id);
                foreach (ExamQuestion question in section.Questions)
                    if (!questionIds.Add(question.Id)) throw new ArgumentException("Duplicate question id: " + question.Id);
                sections.Add(section);
            }
            Sections = sections.AsReadOnly();
            History = new ReadOnlyCollection<ExamResult>(history);
            answers = new int[sections.Count][];
            for (int section = 0; section < sections.Count; section++)
            {
                answers[section] = new int[sections[section].Questions.Count];
                for (int q = 0; q < answers[section].Length; q++) answers[section][q] = -1;
            }
            Stage = ExamStage.Answering;
        }

        public int GetAnswer(int localIndex)
        {
            CheckQuestionIndex(localIndex);
            return answers[SectionIndex][localIndex];
        }

        public string GetPrompt(int localIndex)
        {
            CheckQuestionIndex(localIndex);
            ExamQuestion question = CurrentSection.Questions[localIndex];
            return changedShirts.Contains(question.Id) ? question.ChangedPrompt : question.Prompt;
        }

        public bool OpenBook()
        {
            if (Stage != ExamStage.Answering || BookOpen || NeedsArchiveRecord) return false;
            // This is the only text mutation boundary. Nothing can change a question while it is being read.
            foreach (ExamQuestion question in CurrentSection.Questions)
                if (question.Anomaly == ExamAnomaly.Shirt && rearObservations.Contains(question.Id))
                    changedShirts.Add(question.Id);
            BookOpen = true;
            return true;
        }

        public bool CloseBook()
        {
            if (!BookOpen) return false;
            BookOpen = false;
            return true;
        }

        /// <summary>Call only after this question is visibly presented, not when a hidden page is populated.</summary>
        public bool MarkQuestionViewed(int localIndex)
        {
            if (Stage != ExamStage.Answering || !BookOpen || !IsQuestionIndex(localIndex)) return false;
            ExamQuestion question = CurrentSection.Questions[localIndex];
            if (question.Anomaly == ExamAnomaly.Shirt)
            {
                if (changedShirts.Contains(question.Id)) changedShirtReads.Add(question.Id);
                else initialShirtReads.Add(question.Id);
            }
            return true;
        }

        /// <summary>The presentation layer confirms a clear rear view while the booklet is closed.</summary>
        public bool ObserveBehind()
        {
            if (Stage != ExamStage.Answering || BookOpen) return false;
            bool changed = false;
            foreach (ExamQuestion question in CurrentSection.Questions)
                if (question.Anomaly == ExamAnomaly.Shirt && initialShirtReads.Contains(question.Id))
                    changed |= rearObservations.Add(question.Id);
            return changed;
        }

        public bool SetAnswer(int localIndex, int option)
        {
            if (Stage != ExamStage.Answering || !BookOpen || !IsQuestionIndex(localIndex) || option < 0 || option > 3) return false;
            if (answers[SectionIndex][localIndex] >= 0 && answers[SectionIndex][localIndex] != option) SelectionEdits++;
            answers[SectionIndex][localIndex] = option;
            return true;
        }

        /// <summary>The view confirms the actual archive text has been shown before taking its record.</summary>
        public bool MarkArchiveRead()
        {
            if (!NeedsArchiveRecord || ArchiveRead) return false;
            ArchiveRead = true;
            return true;
        }

        public bool CollectArchiveRecord()
        {
            if (!NeedsArchiveRecord || !ArchiveRead) return false;
            ArchiveCollected = true;
            return true;
        }

        /// <summary>Submitted input, available once inserted; altered machine marks never replace this record.</summary>
        public int GetRecordedAnswer(string questionId)
        {
            if (string.IsNullOrEmpty(questionId)) return -1;
            foreach (ExamResult result in history)
                foreach (ExamResultRow row in result.Rows)
                    if (row.Question.Id == questionId) return row.OriginalAnswer;
            if (Stage == ExamStage.Scanning && pendingAnswers != null)
                for (int i = 0; i < CurrentSection.Questions.Count; i++)
                    if (CurrentSection.Questions[i].Id == questionId) return pendingAnswers[i];
            return -1;
        }

        public string GetRecordedOptionText(string questionId)
        {
            int answer = GetRecordedAnswer(questionId);
            if (answer < 0) return "";
            foreach (ExamSection section in sections)
                foreach (ExamQuestion question in section.Questions)
                    if (question.Id == questionId) return question.Options[answer];
            return "";
        }

        public bool BeginSubmission()
        {
            if (!CanSubmit || BookOpen) return false;
            pendingAnswers = (int[])answers[SectionIndex].Clone();
            pendingPrompts = new string[pendingAnswers.Length];
            for (int i = 0; i < pendingPrompts.Length; i++) pendingPrompts[i] = GetPrompt(i);
            quietElapsed = 0;
            Stage = ExamStage.Scanning;
            return true;
        }

        /// <summary>
        /// Advance only while gameplay is active. Standing holds progress; time never accrues off-stage.
        /// Cap single calls so a stall, focus return or invalid delta cannot skip the interval.
        /// </summary>
        public bool AdvanceQuietInterval(float deltaTime, bool seated)
        {
            if (!NeedsQuietInterval || !seated || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return false;
            quietElapsed = Math.Min(QuietIntervalSeconds, quietElapsed + Math.Min(deltaTime, .25f));
            if (QuietIntervalSeconds - quietElapsed < .0001f) quietElapsed = QuietIntervalSeconds;
            return true;
        }

        public bool FinishScan()
        {
            if (!CanFinishScan) return false;
            history.Add(new ExamResult(SectionIndex, CurrentSection, pendingAnswers, pendingPrompts));
            pendingAnswers = null;
            pendingPrompts = null;
            Stage = ExamStage.ResultsReady;
            return true;
        }

        public bool RetrieveResults()
        {
            if (Stage != ExamStage.ResultsReady) return false;
            Stage = ExamStage.Reviewing;
            return true;
        }

        public bool AdvanceAfterReview()
        {
            if (Stage != ExamStage.Reviewing) return false;
            if (SectionIndex + 1 < sections.Count)
            {
                SectionIndex++;
                Stage = ExamStage.Answering;
            }
            else Stage = ExamStage.AwaitingExit;
            BookOpen = false;
            quietElapsed = 0;
            return true;
        }

        public bool CompleteAtExit()
        {
            if (Stage != ExamStage.AwaitingExit) return false;
            Stage = ExamStage.Complete;
            return true;
        }

        bool IsQuestionIndex(int index) { return index >= 0 && index < CurrentSection.Questions.Count; }
        void CheckQuestionIndex(int index)
        {
            if (!IsQuestionIndex(index)) throw new ArgumentOutOfRangeException("localIndex");
        }
    }
}
