using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TestAnxiety;

internal static class Program
{
    static int passed;
    static int failed;
    static readonly string Source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestAnxietyExam.json"));

    static int Main()
    {
        Run("Answer storage and immutable input", AnswerStorage);
        Run("Shirt read / look / reopen sequence", ShirtTransitions);
        Run("Normal and impossible grading", Grading);
        Run("Changed answer covers all four original marks", ChangedAnswers);
        Run("Archive read and collection gates", ArchiveGates);
        Run("Seated interval, pause and invalid deltas", QuietInterval);
        Run("Recorded answers and deliberate selection changes", RecordsAndEdits);
        Run("Authored per-response notes", ResponseNotes);
        Run("Every release choice remains playable", ReleaseChoices);
        Run("Every phase rejects out-of-order operations", InvalidTransitions);
        Run("All answer routes can finish", CompletionRoutes);
        Run("Catalog order and scenario removal", DataDrivenOrder);
        Run("Catalog validation", InvalidCatalogs);
        Console.WriteLine("TEST ANXIETY: " + passed + " checks passed, " + failed + " failed.");
        Console.WriteLine("Verified pure C# answer storage, immutable submission records, grading, observation/archive/quiet gates and complete examination flow.");
        Console.WriteLine("No Unity Editor, Play Mode, rendering, animation, audio or first-person control verification was performed by this suite.");
        return failed == 0 ? 0 : 1;
    }

    static ExamCatalog Catalog() { return JsonSerializer.Deserialize<ExamCatalog>(Source, new JsonSerializerOptions { IncludeFields = true }); }
    static ExamSession NewSession() { return new ExamSession(Catalog()); }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception); }
    }

    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed++;
    }

    static void Throws<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { passed++; return; }
        throw new InvalidOperationException(description);
    }

    static void AnswerStorage()
    {
        ExamCatalog catalog = Catalog();
        var session = new ExamSession(catalog);
        Check(session.SectionCount == 5 && session.CurrentSection.Questions.Count == 3, "Expected five authored sections with three questions each.");
        Check(session.GetAnswer(0) == -1 && session.AnsweredCount == 0, "Unanswered must be distinguishable from A.");
        Check(!session.SetAnswer(0, 0), "Cannot mark hidden paper.");
        Check(session.OpenBook(), "Book opens.");
        Check(!session.SetAnswer(-1, 0) && !session.SetAnswer(3, 0) && !session.SetAnswer(0, -1) && !session.SetAnswer(0, 4), "Reject invalid question and option indices.");
        Check(session.SetAnswer(0, 3), "Can fill D.");
        Check(session.SetAnswer(1, 0) && session.SetAnswer(2, 2), "Can complete sheet.");
        Check(session.AnsweredCount == 3 && session.CanSubmit, "Readiness is available while booklet remains open.");
        Check(!session.BeginSubmission(), "Physical insertion requires closed book.");
        Check(session.CloseBook() && session.OpenBook(), "Book can be revisited.");
        Check(session.GetAnswer(0) == 3 && session.GetAnswer(1) == 0 && session.GetAnswer(2) == 2, "Marks persist across close and reopen.");
        Check(session.SetAnswer(0, 1) && session.AnsweredCount == 3, "Changing an answer replaces one bubble.");
        catalog.sections[0].questions[0].options[0] = "corrupted";
        catalog.sections[0].questions[0].prompt = "corrupted";
        catalog.sections[0].title = "corrupted";
        Check(session.CurrentSection.Questions[0].Options[0] == "3:15", "Option values are detached from mutable DTO.");
        Check(session.GetPrompt(0) != "corrupted" && session.CurrentSection.Title != "corrupted", "Question and section text are detached from mutable DTO.");
        Throws<NotSupportedException>(() => ((IList<string>)session.CurrentSection.Questions[0].Options)[0] = "bad", "Options must not expose mutable array.");
        Throws<ArgumentOutOfRangeException>(() => session.GetAnswer(10), "Invalid answer read must fail clearly.");
        Throws<ArgumentOutOfRangeException>(() => session.GetPrompt(-1), "Invalid prompt read must fail clearly.");
    }

    static ExamSession Verification()
    {
        var session = NewSession();
        FillAndResolveShirt(session, 0);
        SubmitAndAdvance(session);
        Check(session.SectionIndex == 1 && session.AnsweredCount == 0, "Next section begins with blank answers.");
        return session;
    }

    static void ShirtTransitions()
    {
        var session = Verification();
        Check(!session.ObserveBehind() && !session.RearObserved, "Looking behind before reading does not arm anomaly.");
        Check(!session.MarkQuestionViewed(1), "Hidden question cannot count as read.");
        session.OpenBook();
        string original = session.GetPrompt(1);
        Check(original == "What color is the shirt of the person sitting behind you?", "Original contradiction is authored exactly.");
        Check(!session.ObserveBehind(), "Cannot observe the physical rear while reading paper.");
        for (int i = 0; i < 3; i++) session.SetAnswer(i, 0);
        Check(!session.CanSubmit && session.SubmissionBlockReason.Contains("Read"), "Answering without displaying shirt question cannot skip it.");
        Check(session.MarkQuestionViewed(1) && session.ShirtInitialRead, "Visible original question arms observation.");
        Check(!session.CanSubmit && session.SubmissionBlockReason.Contains("behind"), "Looking behind is required.");
        session.CloseBook();
        Check(session.ObserveBehind() && session.RearObserved, "Rear view after reading is accepted.");
        Check(!session.ObserveBehind(), "Repeated rear view is idempotent.");
        Check(session.GetPrompt(1) == original && !session.ShirtChanged && !session.ShirtPresent, "Observation does not rewrite still-closed booklet immediately.");
        Check(!session.CanSubmit && session.SubmissionBlockReason.Contains("again"), "Reopening and reading changed question is required.");
        session.OpenBook();
        Check(session.ShirtChanged && session.ShirtPresent, "Reopening commits shirt and question together.");
        Check(session.GetPrompt(1) == "What color is the shirt hanging on the wall?", "Changed question is authored exactly.");
        Check(!session.ShirtChangedRead && !session.CanSubmit, "Committed but unseen text is insufficient.");
        Check(session.MarkQuestionViewed(1) && session.ShirtChangedRead && session.CanSubmit, "Visible changed wording completes event gate.");
        Check(session.GetAnswer(1) == 0, "Text anomaly does not alter the player's mark.");
        session.CloseBook();
        session.OpenBook();
        Check(session.GetPrompt(1) != original && session.GetAnswer(1) == 0, "Changed text and mark persist through later visits.");
        Check(!session.ObserveBehind(), "No text updates while actively reading.");
    }

    static void Grading()
    {
        var session = NewSession();
        session.OpenBook();
        for (int i = 0; i < 3; i++) session.SetAnswer(i, session.CurrentSection.Questions[i].CorrectOption);
        session.CloseBook();
        Check(session.BeginSubmission() && session.Stage == ExamStage.Scanning, "Insertion enters Scanning.");
        Check(session.History.Count == 0 && session.LatestResult == null, "Results are absent before scan completion.");
        Check(!session.SetAnswer(0, 3), "Submission freezes marks.");
        Check(session.FinishScan() && session.LatestResult.CorrectCount == 3, "Normal machine establishes truthful grading.");
        Check(session.LatestResult.Rows[0].OriginalAnswer == 0 && session.LatestResult.Rows[0].ReturnedAnswer == 0, "Normal response remains unchanged.");
        Throws<NotSupportedException>(() => ((IList<ExamResultRow>)session.LatestResult.Rows).Clear(), "Graded rows must be immutable.");
        session.RetrieveResults();
        session.AdvanceAfterReview();
        FillAndResolveShirt(session, 0);
        session.BeginSubmission();
        session.FinishScan();
        ExamResultRow alone = session.LatestResult.Rows[0];
        Check(alone.Question.Anomaly == ExamAnomaly.Alone && alone.OriginalAnswer == 0 && !alone.IsCorrect && alone.CorrectAnswer == 1, "YES to being alone is returned red and corrected to NO.");
        Check(alone.Note.Contains("RECOUNT"), "Impossible grade carries atmospheric feedback.");
        Check(session.LatestResult.Rows[1].Prompt.Contains("hanging on the wall"), "Submitted prompt snapshot reflects changed wording.");
        Check(session.History.Count == 2 && session.History[0].CorrectCount == 3, "Prior section remains reviewable and unchanged.");
        Throws<NotSupportedException>(() => ((IList<ExamResult>)session.History).Clear(), "History cannot be mutated by UI.");
    }

    static void ChangedAnswers()
    {
        for (int original = 0; original < 4; original++)
        {
            var session = AtSection("retention");
            int question = QuestionIndex(session, "symbol");
            FillAndResolveShirt(session, original);
            Check(session.GetAnswer(question) == original, "Marked original option is retained before scan.");
            session.BeginSubmission();
            session.FinishScan();
            ExamResultRow row = session.LatestResult.Rows[question];
            Check(row.OriginalAnswer == original && row.ReturnedAnswer == (original + 1) % 4 && row.WasChanged, "Machine selects a different bubble for original " + original + ".");
            Check(!row.IsCorrect, "Changed mark is always visibly red-marked, including when returned mark happens to equal answer key.");
            Check(session.GetAnswer(question) == original, "Original pencil record is never overwritten by returned selection.");
            Check(session.GetRecordedAnswer("symbol") == original && session.GetRecordedOptionText("symbol") == row.Question.Options[original], "Cross-section lookup retains original input after machine alteration.");
            Check(row.Note.Contains("No correction"), "Returned sheet denies its alteration.");
        }
    }

    static void ArchiveGates()
    {
        var session = NewSession();
        Check(!session.NeedsArchiveRecord && !session.MarkArchiveRead() && !session.CollectArchiveRecord(), "Archive cannot be consumed before an authored requirement.");
        session = AtSection("allocation");
        Check(session.NeedsArchiveRecord && !session.ArchiveRead && !session.ArchiveCollected, "Allocation requests a previously unread archive.");
        Check(!session.OpenBook() && !session.CanSubmit && !session.BeginSubmission(), "Unread archive blocks booklet and submission.");
        Check(!session.CollectArchiveRecord(), "An unseen archive cannot be collected.");
        Check(session.MarkArchiveRead() && session.ArchiveRead && !session.ArchiveCollected, "Showing archive text records the read separately from collection.");
        Check(!session.MarkArchiveRead() && !session.OpenBook() && session.NeedsArchiveRecord, "Repeated read is idempotent and does not replace collection.");
        Check(session.CollectArchiveRecord() && session.ArchiveCollected && !session.NeedsArchiveRecord, "Taking the visible record satisfies the prerequisite.");
        Check(!session.CollectArchiveRecord() && !session.MarkArchiveRead(), "Collected archive cannot be consumed twice.");
        Check(session.OpenBook(), "Resolved prerequisite opens the section normally.");
        FillAndResolveShirt(session, 0);
        SubmitAndAdvance(session);
        Check(session.ArchiveRead && session.ArchiveCollected && !session.NeedsArchiveRecord, "Retained archive survives later sections.");
    }

    static void QuietInterval()
    {
        var session = NewSession();
        Check(!session.AdvanceQuietInterval(1, true) && session.QuietProgress == 0 && !session.CanFinishScan, "Quiet time cannot accrue before insertion.");
        session = AtSection("allocation");
        FillAndResolveShirt(session, 0);
        Check(session.BeginSubmission() && session.NeedsQuietInterval && !session.CanFinishScan && session.QuietProgress == 0, "Authored quiet condition begins with insertion.");
        Check(!session.FinishScan() && !session.RetrieveResults(), "Neither motor completion nor retrieval can bypass the interval.");
        Check(!session.AdvanceQuietInterval(float.NaN, true) && !session.AdvanceQuietInterval(float.PositiveInfinity, true) &&
            !session.AdvanceQuietInterval(float.NegativeInfinity, true) && !session.AdvanceQuietInterval(-1, true) && !session.AdvanceQuietInterval(0, true),
            "Invalid or nonpositive deltas are rejected.");
        Check(session.QuietElapsed == 0, "Rejected deltas do not poison interval progress.");
        Check(!session.AdvanceQuietInterval(10, false) && session.QuietProgress == 0, "Walking or standing cannot advance seated observation.");
        Check(session.AdvanceQuietInterval(1000, true) && session.QuietElapsed == .25f, "A large frame delta is capped rather than skipping the wait.");
        for (int i = 0; i < 35; i++) session.AdvanceQuietInterval(.25f, true);
        Check(session.QuietElapsed == 9 && session.QuietProgress == .5f, "Progress describes an eighteen-second interval.");
        float pausedAt = session.QuietElapsed;
        for (int i = 0; i < 100; i++) session.AdvanceQuietInterval(.1f, false);
        Check(session.QuietElapsed == pausedAt, "Standing holds progress without reset or hidden advancement.");
        // A real pause calls nothing: time is wholly caller-driven, with no clock dependency.
        Check(session.QuietElapsed == pausedAt && !session.CanFinishScan, "No simulation step means no progress while paused.");
        for (int i = 0; i < 35; i++) session.AdvanceQuietInterval(.25f, true);
        Check(session.QuietElapsed == 17.75f && !session.CanFinishScan && !session.FinishScan(), "The full interval is required.");
        Check(session.AdvanceQuietInterval(.25f, true) && !session.NeedsQuietInterval && session.CanFinishScan && session.QuietProgress == 1, "Final seated interval releases the scan.");
        Check(!session.AdvanceQuietInterval(.25f, true) && session.QuietProgress == 1, "Completed interval clamps and rejects repeated advancement.");
        Check(session.FinishScan() && !session.FinishScan(), "Only one ready transition follows the interval.");
        session.RetrieveResults();
        session.AdvanceAfterReview();
        Check(session.QuietProgress == 0 && !session.NeedsQuietInterval && !session.AdvanceQuietInterval(1, true), "Later stages cannot inherit quiet progress.");
        FillAndResolveShirt(session, 0);
        Check(session.BeginSubmission() && session.CanFinishScan && !session.NeedsQuietInterval, "Ordinary scans remain ungated by quiet conditions.");
    }

    static void RecordsAndEdits()
    {
        var session = NewSession();
        Check(session.GetRecordedAnswer("clock") == -1 && session.GetRecordedOptionText("clock") == "", "Unsubmitted answers have no recorded lookup.");
        Check(session.GetRecordedAnswer(null) == -1 && session.GetRecordedAnswer("missing") == -1 && session.GetRecordedOptionText("missing") == "", "Unknown record IDs have safe absent values.");
        session.OpenBook();
        session.SetAnswer(0, 0);
        session.SetAnswer(0, 0);
        Check(session.SelectionEdits == 0, "Initial mark and repeated selection do not count as corrections.");
        session.SetAnswer(0, 2);
        session.SetAnswer(0, 1);
        Check(session.SelectionEdits == 2, "Only a changed existing mark increments the correction count.");
        session.SetAnswer(0, 8);
        Check(session.SelectionEdits == 2, "Invalid input cannot inflate correction count.");
        session.SetAnswer(1, 1);
        session.SetAnswer(2, 2);
        Check(session.GetRecordedAnswer("clock") == -1, "Live pencil marks remain distinct from committed records.");
        session.CloseBook();
        session.BeginSubmission();
        Check(session.GetRecordedAnswer("clock") == 1 && session.GetRecordedOptionText("clock") == "9:00", "Pending scan exposes the submitted snapshot for physical reactions.");
        session.SetAnswer(0, 0);
        Check(session.GetRecordedAnswer("clock") == 1 && session.SelectionEdits == 2, "No edit can change a submitted snapshot.");
        session.FinishScan();
        session.RetrieveResults();
        session.AdvanceAfterReview();
        Check(session.GetRecordedAnswer("clock") == 1 && session.GetRecordedOptionText("clock") == "9:00", "Committed answers remain queryable across later sections.");
        session.OpenBook();
        session.SetAnswer(0, 0);
        session.SetAnswer(0, 1);
        Check(session.SelectionEdits == 3, "Correction count persists across sections.");
    }

    static void ReleaseChoices()
    {
        for (int choice = 0; choice < 4; choice++)
        {
            var session = AtSection("release");
            Check(session.FinalReleaseChoice == -1, "No final designation exists before submission.");
            FillAndResolveShirt(session, 0);
            session.OpenBook();
            session.SetAnswer(QuestionIndex(session, "release-choice"), choice);
            session.CloseBook();
            Check(session.FinalReleaseChoice == -1, "A pencilled designation is not yet authorization.");
            Check(session.BeginSubmission() && session.FinalReleaseChoice == choice, "Each inserted designation is available to the ending controller.");
            Check(session.FinishScan() && session.RetrieveResults() && session.AdvanceAfterReview(), "Every designation can be reviewed and accepted.");
            Check(session.Stage == ExamStage.AwaitingExit && session.CompleteAtExit() && session.FinalReleaseChoice == choice, "Designation " + choice + " reaches a stable ending regardless of grade.");
        }
    }

    static void ResponseNotes()
    {
        for (int answer = 0; answer < 4; answer++)
        {
            var session = AtSection("allocation");
            int question = QuestionIndex(session, "allocation-seat");
            FillAndResolveShirt(session, answer);
            string expected = session.CurrentSection.Questions[question].ResponseNotes[answer];
            session.BeginSubmission();
            ResolveQuiet(session);
            session.FinishScan();
            Check(session.LatestResult.Rows[question].Note == expected, "Authored note follows submitted allocation option " + answer + ".");
            if (answer == 3) Check(expected.Contains("No vacancy selected") && expected.Contains("06"), "Refusal note distinguishes player's choice from administrative allocation.");
        }

        ExamCatalog catalog = Catalog();
        QuestionDefinition authored = catalog.sections[3].questions[1];
        authored.responseNotes = new[] { "Recorded A", "Recorded B", "Recorded C", "Recorded D" };
        catalog.sections = new[] { catalog.sections[3] };
        var changed = new ExamSession(catalog);
        authored.responseNotes[2] = "corrupted";
        int changedIndex = QuestionIndex(changed, "symbol");
        ExamQuestion frozen = changed.CurrentSection.Questions[changedIndex];
        Check(frozen.ResponseNotes[2] == "Recorded C", "Response notes are detached from mutable authoring arrays.");
        Throws<NotSupportedException>(() => ((IList<string>)frozen.ResponseNotes)[2] = "bad", "Response note collection must be immutable.");
        FillAndResolveShirt(changed, 2);
        changed.BeginSubmission();
        changed.FinishScan();
        ExamResultRow row = changed.LatestResult.Rows[changedIndex];
        Check(row.OriginalAnswer == 2 && row.ReturnedAnswer == 3 && row.Note == "Recorded C", "Response note uses original selection even when machine alters returned bubble.");
        ExamResultRow fallback = changed.LatestResult.Rows[0];
        Check(fallback.Question.ResponseNotes.Count == 0 && fallback.Note == fallback.Question.ExaminerNote, "Questions without response notes retain their ordinary examiner note.");
    }

    static void InvalidTransitions()
    {
        var session = NewSession();
        Check(!session.FinishScan() && !session.RetrieveResults() && !session.AdvanceAfterReview() && !session.CompleteAtExit(), "Answering cannot skip to later phases.");
        Check(!session.BeginSubmission(), "Blank paper cannot submit.");
        FillAndResolveShirt(session, 0);
        Check(session.BeginSubmission(), "Ready paper starts one scan.");
        Check(!session.BeginSubmission() && !session.OpenBook() && !session.RetrieveResults() && !session.AdvanceAfterReview() && !session.CompleteAtExit(), "Scanning rejects duplicate insertion and premature results/exit.");
        Check(session.FinishScan(), "One scan completes.");
        Check(!session.FinishScan() && !session.BeginSubmission() && !session.AdvanceAfterReview() && !session.OpenBook() && !session.CompleteAtExit(), "Ready paper must be physically retrieved before advancement.");
        Check(session.History.Count == 1, "Duplicate completion cannot append result twice.");
        Check(session.RetrieveResults(), "Result can be picked up.");
        Check(!session.RetrieveResults() && !session.FinishScan() && !session.BeginSubmission() && !session.OpenBook() && !session.CompleteAtExit(), "Reviewing rejects duplicate pick-up and premature completion.");
        session.AdvanceAfterReview();
        Check(!session.AdvanceAfterReview(), "Double continue does not skip a section.");
        while (session.Stage == ExamStage.Answering) { FillAndResolveShirt(session, 0); SubmitAndAdvance(session); }
        Check(session.Stage == ExamStage.AwaitingExit, "Last review arms exit.");
        Check(!session.OpenBook() && !session.BeginSubmission() && !session.FinishScan() && !session.RetrieveResults() && !session.AdvanceAfterReview(), "AwaitingExit cannot restart completed work.");
        Check(session.CompleteAtExit() && session.Stage == ExamStage.Complete, "Exit completes examination.");
        Check(!session.CompleteAtExit() && !session.OpenBook() && !session.BeginSubmission() && !session.AdvanceAfterReview(), "Ending is stable and idempotent.");
    }

    static void CompletionRoutes()
    {
        // Every possible A/B/C/D combination for a three-question sheet, across each stage.
        for (int route = 0; route < 64; route++)
        {
            var session = NewSession();
            for (int section = 0; section < session.SectionCount; section++)
            {
                FillAndResolveShirt(session, 0);
                session.OpenBook();
                for (int q = 0; q < 3; q++) session.SetAnswer(q, (route >> (q * 2)) & 3);
                session.CloseBook();
                Check(session.CanSubmit, "Any completed answer combination remains submittable.");
                SubmitAndAdvance(session);
            }
            Check(session.History.Count == session.SectionCount && session.Stage == ExamStage.AwaitingExit && session.CompleteAtExit(), "Route " + route + " finishes regardless of grade.");
        }
    }

    static void DataDrivenOrder()
    {
        ExamCatalog catalog = Catalog();
        Array.Reverse(catalog.sections);
        foreach (SectionDefinition section in catalog.sections) Array.Reverse(section.questions);
        var session = new ExamSession(catalog);
        for (int i = 0; i < session.SectionCount; i++) { FillAndResolveShirt(session, 2); SubmitAndAdvance(session); }
        Check(session.CompleteAtExit() && RecordedRow(session, "symbol").WasChanged && session.ArchiveCollected, "Events follow authored question IDs/types after section and question reordering.");

        catalog = Catalog();
        catalog.sections = new[] { catalog.sections[0] };
        session = new ExamSession(catalog);
        FillAndResolveShirt(session, 0);
        SubmitAndAdvance(session);
        Check(session.CompleteAtExit() && !session.ShirtChanged, "Removing anomaly sections does not leave an event gate.");

        catalog = Catalog();
        catalog.sections[1].questions = new[] { catalog.sections[1].questions[1] };
        session = new ExamSession(catalog);
        for (int i = 0; i < session.SectionCount; i++) { FillAndResolveShirt(session, 1); SubmitAndAdvance(session); }
        Check(session.CompleteAtExit() && session.History[1].Rows.Count == 1, "Variable question counts do not rely on fixed indices.");

        catalog = Catalog();
        QuestionDefinition firstShirt = catalog.sections[1].questions[1];
        catalog.sections[2].questions[1] = new QuestionDefinition {
            id = "shirt-again", prompt = firstShirt.prompt, changedPrompt = firstShirt.changedPrompt,
            options = (string[])firstShirt.options.Clone(), correctOption = firstShirt.correctOption,
            anomaly = firstShirt.anomaly, examinerNote = firstShirt.examinerNote
        };
        session = new ExamSession(catalog);
        for (int i = 0; i < 2; i++) { FillAndResolveShirt(session, 0); SubmitAndAdvance(session); }
        Check(session.RearObserved && session.ShirtChangedRead && !session.NeedsRearObservation && !session.NeedsShirtRevisit,
            "A prior shirt event does not mark an unread later scenario as pending.");
        ResolveArchive(session);
        session.OpenBook();
        session.MarkQuestionViewed(1);
        Check(session.NeedsRearObservation && !session.NeedsShirtRevisit, "A second authored shirt question independently requests rear observation.");
        session.CloseBook();
        session.ObserveBehind();
        Check(!session.NeedsRearObservation && session.NeedsShirtRevisit, "Second event guides player back to booklet independently.");
        session.OpenBook();
        Check(session.NeedsShirtRevisit, "Second changed prompt still requires a visible read.");
        session.MarkQuestionViewed(1);
        Check(!session.NeedsRearObservation && !session.NeedsShirtRevisit, "Second event clears its own guidance after visible read.");
        for (int i = 0; i < 3; i++) session.SetAnswer(i, 1);
        session.CloseBook();
        SubmitAndAdvance(session);
        while (session.Stage == ExamStage.Answering) { FillAndResolveShirt(session, 0); SubmitAndAdvance(session); }
        Check(session.CompleteAtExit(), "Multiple authored shirt events in separate sections can finish.");
    }

    static void InvalidCatalogs()
    {
        Throws<ArgumentException>(() => new ExamSession(null), "Null catalog must fail clearly.");
        Throws<ArgumentException>(() => new ExamSession(new ExamCatalog()), "Missing sections must fail clearly.");
        Throws<ArgumentException>(() => new ExamSession(new ExamCatalog { sections = Array.Empty<SectionDefinition>() }), "Empty examination must fail clearly.");
        BadCatalog(c => c.sections[0] = null, "Null section");
        BadCatalog(c => c.sections[0].id = "", "Empty section ID");
        BadCatalog(c => c.sections[1].id = c.sections[0].id, "Duplicate section ID");
        BadCatalog(c => c.sections[0].questions = null, "Missing question array");
        BadCatalog(c => c.sections[0].questions[0] = null, "Null question");
        BadCatalog(c => c.sections[0].questions[0].prompt = "", "Empty prompt");
        BadCatalog(c => c.sections[0].questions[1].id = c.sections[0].questions[0].id, "Duplicate question ID");
        BadCatalog(c => c.sections[0].questions[0].options = new[] { "one" }, "Wrong number of options");
        BadCatalog(c => c.sections[0].questions[0].options[0] = " ", "Empty answer option");
        BadCatalog(c => c.sections[0].questions[0].correctOption = 4, "Invalid answer key");
        BadCatalog(c => c.sections[0].questions[0].anomaly = "typo", "Unknown event type");
        BadCatalog(c => c.sections[1].questions[1].changedPrompt = "", "Missing changed question");
        BadCatalog(c => c.sections[0].prerequisite = "typo", "Unknown section prerequisite");
        BadCatalog(c => c.sections[0].scanCondition = "typo", "Unknown scan condition");
        BadCatalog(c => c.sections[0].questions[0].responseNotes = new[] { "A", "B", "C" }, "Wrong response note count");
        BadCatalog(c => c.sections[0].questions[0].responseNotes = new string[0], "Present but empty response note array");
        BadCatalog(c => c.sections[0].questions[0].responseNotes = new[] { "A", "B", " ", "D" }, "Empty response note");
    }

    static void BadCatalog(Action<ExamCatalog> mutate, string description)
    {
        ExamCatalog catalog = Catalog();
        mutate(catalog);
        Throws<ArgumentException>(() => new ExamSession(catalog), description + " must fail validation.");
    }

    static void FillAndResolveShirt(ExamSession session, int answer)
    {
        ResolveArchive(session);
        session.OpenBook();
        bool hasShirt = false;
        for (int i = 0; i < session.CurrentSection.Questions.Count; i++)
        {
            session.MarkQuestionViewed(i);
            session.SetAnswer(i, answer);
            hasShirt |= session.CurrentSection.Questions[i].Anomaly == ExamAnomaly.Shirt;
        }
        session.CloseBook();
        if (hasShirt)
        {
            session.ObserveBehind();
            session.OpenBook();
            for (int i = 0; i < session.CurrentSection.Questions.Count; i++) session.MarkQuestionViewed(i);
            session.CloseBook();
        }
    }

    static void SubmitAndAdvance(ExamSession session)
    {
        Check(session.BeginSubmission(), "Complete section submits.");
        ResolveQuiet(session);
        Check(session.FinishScan(), "Mechanical completion produces result.");
        Check(session.RetrieveResults(), "Returned paper can be retrieved.");
        Check(session.AdvanceAfterReview(), "Any grade permits next section or exit.");
    }

    static void ResolveArchive(ExamSession session)
    {
        if (!session.NeedsArchiveRecord) return;
        session.MarkArchiveRead();
        Check(session.CollectArchiveRecord(), "Read archive can be collected when required.");
    }

    static void ResolveQuiet(ExamSession session)
    {
        // An upper bound turns a broken interval transition into a failed test instead of hanging.
        for (int i = 0; i < 73 && session.NeedsQuietInterval; i++) session.AdvanceQuietInterval(.25f, true);
        Check(session.CanFinishScan, "Every submitted section's environmental requirements can finish.");
    }

    static ExamSession AtSection(string id)
    {
        var session = NewSession();
        while (session.CurrentSection.Id != id && session.Stage == ExamStage.Answering)
        {
            FillAndResolveShirt(session, 0);
            SubmitAndAdvance(session);
        }
        Check(session.Stage == ExamStage.Answering && session.CurrentSection.Id == id, "Authored section is reachable by ID: " + id);
        return session;
    }

    static int QuestionIndex(ExamSession session, string id)
    {
        for (int i = 0; i < session.CurrentSection.Questions.Count; i++)
            if (session.CurrentSection.Questions[i].Id == id) return i;
        throw new InvalidOperationException("Question not present: " + id);
    }

    static ExamResultRow RecordedRow(ExamSession session, string id)
    {
        foreach (ExamResult result in session.History)
            foreach (ExamResultRow row in result.Rows)
                if (row.Question.Id == id) return row;
        throw new InvalidOperationException("Recorded question not present: " + id);
    }
}
