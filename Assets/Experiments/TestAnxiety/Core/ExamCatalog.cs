using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TestAnxiety
{
    // Field-based authoring DTOs are intentionally compatible with Unity JsonUtility.
    [Serializable]
    public sealed class ExamCatalog
    {
        public SectionDefinition[] sections;
    }

    [Serializable]
    public sealed class SectionDefinition
    {
        public string id;
        public string title;
        public string instructions;
        public string prerequisite;
        public string scanCondition;
        public QuestionDefinition[] questions;
    }

    [Serializable]
    public sealed class QuestionDefinition
    {
        public string id;
        public string prompt;
        public string changedPrompt;
        public string[] options;
        public int correctOption;
        public string anomaly;
        public string examinerNote;
        public string[] responseNotes;
    }

    public enum ExamAnomaly { None, Alone, Shirt, ChangedAnswer }

    /// <summary>Validated, immutable copy of an authored question.</summary>
    public sealed class ExamQuestion
    {
        public string Id { get; private set; }
        public string Prompt { get; private set; }
        public string ChangedPrompt { get; private set; }
        public IReadOnlyList<string> Options { get; private set; }
        public int CorrectOption { get; private set; }
        public ExamAnomaly Anomaly { get; private set; }
        public string ExaminerNote { get; private set; }
        public IReadOnlyList<string> ResponseNotes { get; private set; }

        internal ExamQuestion(QuestionDefinition source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.id) || string.IsNullOrWhiteSpace(source.prompt))
                throw new ArgumentException("Every question needs a nonempty id and prompt.");
            if (source.options == null || source.options.Length != 4)
                throw new ArgumentException("Question " + source.id + " must have four answer options.");
            foreach (string option in source.options)
                if (string.IsNullOrWhiteSpace(option)) throw new ArgumentException("Question " + source.id + " has an empty option.");
            if (source.correctOption < 0 || source.correctOption > 3)
                throw new ArgumentException("Question " + source.id + " has an invalid correctOption.");
            if (source.responseNotes != null)
            {
                if (source.responseNotes.Length != 4)
                    throw new ArgumentException("Question " + source.id + " must supply four responseNotes when present.");
                foreach (string note in source.responseNotes)
                    if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Question " + source.id + " has an empty response note.");
            }

            ExamAnomaly anomaly;
            switch (source.anomaly ?? "none")
            {
                case "none": case "": anomaly = ExamAnomaly.None; break;
                case "alone": anomaly = ExamAnomaly.Alone; break;
                case "shirt": anomaly = ExamAnomaly.Shirt; break;
                case "changed-answer": anomaly = ExamAnomaly.ChangedAnswer; break;
                default: throw new ArgumentException("Unknown anomaly '" + source.anomaly + "' on " + source.id + ".");
            }
            if (anomaly == ExamAnomaly.Shirt && string.IsNullOrWhiteSpace(source.changedPrompt))
                throw new ArgumentException("A shirt question requires changedPrompt.");

            Id = source.id;
            Prompt = source.prompt;
            ChangedPrompt = source.changedPrompt ?? "";
            Options = new ReadOnlyCollection<string>((string[])source.options.Clone());
            CorrectOption = source.correctOption;
            Anomaly = anomaly;
            ExaminerNote = source.examinerNote ?? "";
            ResponseNotes = new ReadOnlyCollection<string>(source.responseNotes == null ? new string[0] : (string[])source.responseNotes.Clone());
        }
    }

    public sealed class ExamSection
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Instructions { get; private set; }
        public string Prerequisite { get; private set; }
        public string ScanCondition { get; private set; }
        public bool RequiresArchive { get { return Prerequisite == "archive"; } }
        public bool RequiresQuiet { get { return ScanCondition == "quiet"; } }
        public IReadOnlyList<ExamQuestion> Questions { get; private set; }

        internal ExamSection(SectionDefinition source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.id) || string.IsNullOrWhiteSpace(source.title))
                throw new ArgumentException("Every section needs a nonempty id and title.");
            if (source.questions == null || source.questions.Length == 0)
                throw new ArgumentException("Section " + source.id + " has no questions.");
            string prerequisite = string.IsNullOrEmpty(source.prerequisite) ? "none" : source.prerequisite;
            string scanCondition = string.IsNullOrEmpty(source.scanCondition) ? "none" : source.scanCondition;
            if (prerequisite != "none" && prerequisite != "archive")
                throw new ArgumentException("Unknown prerequisite '" + prerequisite + "' on section " + source.id + ".");
            if (scanCondition != "none" && scanCondition != "quiet")
                throw new ArgumentException("Unknown scanCondition '" + scanCondition + "' on section " + source.id + ".");
            Id = source.id;
            Title = source.title;
            Instructions = source.instructions ?? "";
            Prerequisite = prerequisite;
            ScanCondition = scanCondition;
            var questions = new List<ExamQuestion>();
            foreach (QuestionDefinition question in source.questions) questions.Add(new ExamQuestion(question));
            Questions = questions.AsReadOnly();
        }
    }
}
