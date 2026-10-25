using UnityEngine;

namespace TestAnxiety
{
    public sealed partial class TestAnxietyGame
    {
        static readonly Color Paper = new Color(.90f, .88f, .80f);
        static readonly Color Ink = new Color(.15f, .18f, .17f);
        static readonly Color Muted = new Color(.39f, .42f, .38f);
        static readonly Color Red = new Color(.60f, .12f, .10f);
        static readonly Color White = new Color(.91f, .93f, .86f);
        GUIStyle body, small, heading, title, button, centered, tiny;
        Texture2D circleTexture, ringTexture;
        int bookPage, reportPage;
        const int PageSize = 3;

        void UIStyles()
        {
            if (body != null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, wordWrap = true, richText = false, normal = { textColor = Ink } };
            small = new GUIStyle(body) { fontSize = 16 };
            tiny = new GUIStyle(body) { fontSize = 13 };
            heading = new GUIStyle(body) { fontSize = 27, fontStyle = FontStyle.Bold };
            title = new GUIStyle(body) { fontSize = 49, fontStyle = FontStyle.Bold };
            button = new GUIStyle(body) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            circleTexture = MakeCircle(false);
            ringTexture = MakeCircle(true);
        }

        static Texture2D MakeCircle(bool ring)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Test Anxiety pencil bubble", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32));
                    float alpha = Mathf.Clamp01(31 - distance);
                    if (ring) alpha *= Mathf.Clamp01(distance - 26);
                    // Slightly uneven graphite density, deterministic and purely cosmetic.
                    if (!ring) alpha *= .79f + .2f * Mathf.Abs(Mathf.Sin(x * 3.1f + y * .7f));
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void OnGUI()
        {
            UIStyles();
            Matrix4x4 matrix = GUI.matrix;
            Color oldColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 800 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            if (fatalError != null)
            {
                Fill(new Rect(140, 200, 1000, 360), Paper);
                Text(new Rect(180, 235, 900, 50), "TEST ANXIETY / SETUP", heading);
                Text(new Rect(180, 305, 900, 150), fatalError + "\nOpen the supplied scene using Test Anxiety > Open Prototype Scene. Check the Console for details.", body);
            }
            else if (!started) IntroUI();
            else if (Session.Stage == ExamStage.Complete) EndingUI();
            else if (paused) PauseUI();
            else if (finaleActive) { }
            else if (mode == ViewMode.Book && transition > .97f) BookUI();
            else if (mode == ViewMode.Report && transition > .97f) ReportUI();
            else if (mode == ViewMode.Archive && transition > .97f) ArchiveUI();
            else if (mode == ViewMode.Document && transition > .97f) DocumentUI();
            else if (mode == ViewMode.Seated) SeatedUI();
            else if (mode == ViewMode.Walk) WalkUI();
            if (started && !paused && fatalError == null) SequenceOverlayUI();
            GUI.color = oldColor;
            GUI.matrix = matrix;
        }

        void Text(Rect rect, string value, GUIStyle style, Color? color = null)
        {
            Color previous = GUI.color;
            GUI.color = Color.white;
            Color prior = style.normal.textColor;
            style.normal.textColor = color ?? Ink;
            GUI.Label(rect, value, style);
            style.normal.textColor = prior;
            GUI.color = previous;
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        bool ActionButton(Rect rect, string value, bool available = true, bool dark = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition) && available;
            Color background = dark ? new Color(.18f, .25f, .24f) : new Color(.77f, .78f, .70f);
            if (hover) background = dark ? new Color(.27f, .36f, .32f) : new Color(.69f, .73f, .65f);
            if (!available) background = new Color(.82f, .82f, .76f);
            Fill(rect, background);
            Text(rect, value, button, dark ? White : available ? Ink : Muted);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && available;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = wasEnabled;
            return clicked;
        }

        void IntroUI()
        {
            Fill(new Rect(72, 98, 650, 615), new Color(.06f, .10f, .095f, .96f));
            Text(new Rect(108, 133, 570, 35), "DISTRICT ASSESSMENT OFFICE  /  2000", small, White);
            Text(new Rect(103, 186, 600, 77), "TEST ANXIETY", title, White);
            Text(new Rect(108, 271, 562, 70), "Candidate 014.\nPlease take the desk assigned to you.", heading, White);
            Text(new Rect(108, 365, 558, 157), "Your materials are on the center desk in the back row. Read the booklet. Observe the room. Return each completed sheet to the optical reader.\n\nKeep the copies you are given.", body, White);
            Text(new Rect(108, 532, 550, 50), "WASD  walk     MOUSE  look     E  interact\nESC  close paper / pause     CLICK  fill a bubble", small, White);
            if (ActionButton(new Rect(108, 611, 568, 56), "BEGIN EXAMINATION", true, true)) Begin();
            Text(new Rect(108, 680, 550, 24), "Headphones recommended. H offers a contextual hint.", tiny, White);
        }

        void WalkUI()
        {
            Fill(new Rect(632, 393, 5, 5), new Color(.92f, .94f, .87f, .72f));
            Fill(new Rect(32, 30, 386, 56), new Color(.06f, .09f, .08f, .76f));
            Text(new Rect(47, 39, 365, 35), Session.Stage == ExamStage.AwaitingExit ? "EXAMINATION COMPLETE" : Session.CurrentSection.Title, small, White);
            if (elapsed < hintUntil)
            {
                Fill(new Rect(32, 93, 650, 77), new Color(.06f, .09f, .08f, .80f));
                Text(new Rect(47, 101, 620, 65), CurrentHint(), small, White);
            }

            string target = NearbyTarget();
            string prompt = "";
            if (target == "desk") prompt = Session.NeedsQuietInterval ? "E   TAKE YOUR SEAT" : Session.Stage == ExamStage.Answering ? "E   OPEN EXAMINATION BOOKLET" : "E   REVIEW EXAMINATION";
            if (target == "machine")
                prompt = Session.NeedsQuietInterval ? "RETURN TO DESK 014 / TAKE YOUR SEAT" : Scanner.Busy ? "PROCESSING  /  PLEASE WAIT" : Session.Stage == ExamStage.ResultsReady ? "E   RETRIEVE GRADED SHEET" :
                    Session.Stage == ExamStage.Reviewing || Session.Stage == ExamStage.AwaitingExit ? "E   REVIEW RETURNED SHEET" : carrying ? "E   INSERT ANSWER SHEET" : "OPTICAL READER  /  COMPLETED SHEETS ONLY";
            if (target == "exit") prompt = "E   TRY ENTRANCE DOOR";
            if (target == "archive") prompt = "E   INSPECT RETAINED RECORD";
            if (target == "outer-exit") prompt = "E   USE OUTER DOOR";
            if (target == "switch") prompt = "E   ISOLATE READER POWER";
            if (target.StartsWith("document:", System.StringComparison.Ordinal)) prompt = "E   READ DOCUMENT";
            if (Cursor.lockState != CursorLockMode.Locked) prompt = "CLICK TO RETURN TO THE ROOM";
            if (prompt.Length > 0)
            {
                Fill(new Rect(300, 662, 680, 49), new Color(.055f, .085f, .075f, .87f));
                Text(new Rect(310, 665, 660, 43), prompt, centered, White);
            }
            if (elapsed < messageUntil)
            {
                Fill(new Rect(190, 724, 900, 50), new Color(.055f, .085f, .075f, .88f));
                Text(new Rect(207, 728, 866, 42), message, centered, White);
            }
            else Text(new Rect(727, 750, 520, 26), Session.ArchiveCollected ? "E  INTERACT    R  RETAINED COPY    H  HINT    ESC  PAUSE" : "E  INTERACT    H  HINT    ESC  PAUSE", tiny, White);
            if (carrying)
            {
                Fill(new Rect(1100, 587, 180, 124), Paper);
                Text(new Rect(1113, 596, 157, 32), "ANSWER RECORD", tiny);
                for (int i = 0; i < Mathf.Min(Session.CurrentSection.Questions.Count, 3); i++)
                    DrawBubble(new Rect(1120 + Session.GetAnswer(i) * 29, 634 + i * 21, 13, 13), true, Ink);
            }
        }

        int QuestionNumber(int section, int local)
        {
            int number = local + 1;
            for (int i = 0; i < section; i++) number += Session.Sections[i].Questions.Count;
            return number;
        }

        void PaperFrame(string subtitle, string rightTitle)
        {
            Fill(new Rect(59, 40, 1170, 731), new Color(.015f, .025f, .02f, .6f));
            Fill(new Rect(48, 28, 1168, 730), Paper);
            Fill(new Rect(766, 28, 5, 730), new Color(.68f, .67f, .60f));
            Fill(new Rect(771, 28, 444, 730), new Color(.94f, .92f, .84f));
            Text(new Rect(79, 48, 655, 28), "DISTRICT STANDARDIZED OBSERVATION TEST", small);
            Text(new Rect(79, 82, 655, 43), subtitle, heading);
            Text(new Rect(795, 49, 391, 30), rightTitle, small);
            Text(new Rect(795, 85, 391, 35), "CANDIDATE 014", heading);
            Fill(new Rect(80, 132, 650, 1), Muted);
            Fill(new Rect(797, 132, 386, 1), Muted);
        }

        void BookUI()
        {
            PaperFrame(Session.CurrentSection.Title, "FORM 107 / ANSWER RECORD");
            Text(new Rect(80, 145, 650, 54), Session.CurrentSection.Instructions, small, Muted);
            Text(new Rect(800, 145, 380, 48), "Fill one circle per row.\nYou may change a mark before insertion.", small, Muted);
            int count = Session.CurrentSection.Questions.Count;
            bookPage = Mathf.Clamp(bookPage, 0, (count - 1) / PageSize);
            int start = bookPage * PageSize;
            for (int i = start; i < Mathf.Min(start + PageSize, count); i++)
            {
                int local = i - start;
                float y = 211 + local * 140;
                ExamQuestion question = Session.CurrentSection.Questions[i];
                Text(new Rect(81, y, 645, 52), QuestionNumber(Session.SectionIndex, i).ToString("00") + ".  " + Session.GetPrompt(i), body);
                for (int option = 0; option < 4; option++)
                {
                    var optionRect = new Rect(102 + (option % 2) * 319, y + 58 + (option / 2) * 31, 307, 30);
                    Text(optionRect, ((char)('A' + option)) + "   " + question.Options[option], small);
                    if (GUI.Button(optionRect, GUIContent.none, GUIStyle.none) && Session.SetAnswer(i, option)) sound.PlayPencil();
                }
                Text(new Rect(800, y + 3, 65, 40), QuestionNumber(Session.SectionIndex, i).ToString("00"), heading);
                for (int option = 0; option < 4; option++)
                {
                    float x = 853 + option * 77;
                    Text(new Rect(x, y - 2, 42, 26), ((char)('A' + option)).ToString(), centered);
                    var hit = new Rect(x - 8, y + 28, 59, 55);
                    if (hit.Contains(Event.current.mousePosition)) Fill(hit, new Color(.82f, .83f, .75f));
                    DrawBubble(new Rect(x + 1, y + 35, 37, 37), Session.GetAnswer(i) == option, Ink);
                    if (GUI.Button(hit, GUIContent.none, GUIStyle.none) && Session.SetAnswer(i, option)) sound.PlayPencil();
                }
                Text(new Rect(850, y + 91, 315, 25), Session.GetAnswer(i) < 0 ? "UNMARKED" : "Recorded: " + (char)('A' + Session.GetAnswer(i)), tiny, Muted);
                Fill(new Rect(80, y + 130, 650, 1), new Color(.75f, .74f, .67f));
                if (Event.current.type == EventType.Repaint) Session.MarkQuestionViewed(i);
            }
            PageButtons(ref bookPage, count, 80, 638);
            Text(new Rect(800, 635, 380, 47), Session.CanSubmit ? "All responses recorded. Take this sheet to the optical reader." : Session.SubmissionBlockReason, small, Session.CanSubmit ? Ink : Red);
            if (ActionButton(new Rect(800, 693, 384, 44), "TAKE ANSWER SHEET", Session.CanSubmit)) TakeSheet();
            if (ActionButton(new Rect(80, 693, Session.ArchiveCollected ? 232 : 314, 44), "CLOSE  [E / ESC]")) CloseInspection();
            if (Session.History.Count > 0 && ActionButton(new Rect(Session.ArchiveCollected ? 324 : 407, 693, Session.ArchiveCollected ? 222 : 325, 44), "PREVIOUS RETURNS")) { reportPage = 0; OpenReport(true); }
            else if (Session.History.Count == 0) Text(new Rect(410, 700, 320, 29), "Your marks remain on the sheet.", tiny, Muted);
            if (Session.ArchiveCollected && ActionButton(new Rect(558, 693, 174, 44), "RETAINED COPY")) ShowRetainedRecord(true);
        }

        void DrawBubble(Rect rect, bool selected, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, ringTexture);
            if (selected) GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), circleTexture);
            GUI.color = previous;
        }

        void PageButtons(ref int page, int total, float x, float y)
        {
            int pageCount = Mathf.CeilToInt(total / (float)PageSize);
            if (pageCount <= 1) return;
            if (ActionButton(new Rect(x, y, 145, 35), "< PREVIOUS", page > 0)) page--;
            Text(new Rect(x + 161, y + 2, 250, 32), "PAGE " + (page + 1) + " / " + pageCount, small);
            if (ActionButton(new Rect(x + 440, y, 210, 35), "NEXT PAGE >", page + 1 < pageCount)) page++;
        }

        void ReportUI()
        {
            ExamResult result = Session.History[reportIndex];
            reportPage = Mathf.Clamp(reportPage, 0, (result.Rows.Count - 1) / PageSize);
            PaperFrame(result.Title, "OPTICAL READER / RETURNED RECORD");
            Text(new Rect(80, 145, 650, 48), compareCopy ? "CARBON TRANSFER VISIBLE\nOriginal marks are retained beneath the reader's record." : "Return copy / retain for your records.\nA red X indicates an uncredited response.", small, Muted);
            Text(new Rect(800, 145, 380, 46), "ACCEPTED FOR RECORD\n" + result.CorrectCount + " / " + result.Rows.Count + " responses credited", small, Red);
            int start = reportPage * PageSize;
            for (int i = start; i < Mathf.Min(start + PageSize, result.Rows.Count); i++)
            {
                ExamResultRow row = result.Rows[i];
                float y = 211 + (i - start) * 140;
                Text(new Rect(81, y, 645, 50), QuestionNumber(result.SectionIndex, i).ToString("00") + ".  " + row.Prompt, body);
                Text(new Rect(102, y + 55, 620, 30), "Returned mark: " + (char)('A' + row.ReturnedAnswer) + " / " + row.Question.Options[row.ReturnedAnswer], small);
                string note = compareCopy ? "Carbon transfer: " + (char)('A' + row.OriginalAnswer) + " / " + row.Question.Options[row.OriginalAnswer] :
                    row.IsCorrect ? "CREDITED" : "Reader expects " + (char)('A' + row.CorrectAnswer) + " / " + row.Question.Options[row.CorrectAnswer];
                Text(new Rect(102, y + 87, 620, 44), note, small, compareCopy && row.WasChanged || !row.IsCorrect ? Red : Muted);
                Text(new Rect(800, y + 3, 55, 40), QuestionNumber(result.SectionIndex, i).ToString("00"), heading);
                for (int option = 0; option < 4; option++)
                {
                    float x = 860 + option * 62;
                    Text(new Rect(x, y, 35, 25), ((char)('A' + option)).ToString(), small);
                    DrawBubble(new Rect(x - 2, y + 30, 31, 31), row.ReturnedAnswer == option, Ink);
                }
                Text(new Rect(1124, y + 23, 59, 50), row.IsCorrect ? "OK" : "X", heading, row.IsCorrect ? Muted : Red);
                Text(new Rect(802, y + 74, 380, 55), row.Note, small, row.IsCorrect ? Muted : Red);
                Fill(new Rect(80, y + 130, 650, 1), new Color(.75f, .74f, .67f));
            }
            PageButtons(ref reportPage, result.Rows.Count, 80, 638);
            if (historicalReport && Session.History.Count > 1)
            {
                if (ActionButton(new Rect(800, 637, 185, 38), "< EARLIER", reportIndex > 0)) { reportIndex--; reportPage = 0; }
                if (ActionButton(new Rect(997, 637, 185, 38), "LATER >", reportIndex + 1 < Session.History.Count)) { reportIndex++; reportPage = 0; }
            }
            else Text(new Rect(802, 631, 380, 55), ReportInstruction(result), small, Red);
            if (ActionButton(new Rect(80, 693, 310, 44), "LOWER PAPER  [E / ESC]")) CloseInspection();
            if (ActionButton(new Rect(404, 693, 326, 44), compareCopy ? "COVER CARBON COPY" : "COMPARE CARBON COPY")) { compareCopy = !compareCopy; sound.PlayPaper(); }
            if (!historicalReport && Session.Stage == ExamStage.Reviewing)
            {
                string action = Session.SectionIndex + 1 < Session.SectionCount ? "ACCEPT & CONTINUE EXAM" : "ACKNOWLEDGE FINAL RECORD";
                if (ActionButton(new Rect(800, 693, 384, 44), action)) AdvanceReport();
            }
            else if (ActionButton(new Rect(800, 693, 384, 44), "RETURN TO ROOM")) CloseInspection();
        }

        void PauseUI()
        {
            Fill(new Rect(330, 65, 620, 675), Paper);
            Text(new Rect(374, 98, 532, 55), "EXAMINATION PAUSED", heading);
            Text(new Rect(374, 171, 532, 138), "WASD  walk    Mouse / arrows  look\nE  interact or lower paper\nR  retained copy (when collected)\nH  contextual hint\nEscape  lower paper, then pause", body);
            Text(new Rect(374, 323, 532, 29), "Mouse sensitivity", small);
            sensitivity = GUI.HorizontalSlider(new Rect(376, 361, 525, 24), sensitivity, .035f, .22f);
            Text(new Rect(374, 396, 532, 29), "Sound volume", small);
            sound.SetVolume(GUI.HorizontalSlider(new Rect(376, 434, 525, 24), sound.MasterVolume, 0, 1));
            if (ActionButton(new Rect(374, 476, 532, 41), soundCaptions ? "SOUND CAPTIONS: ON" : "SOUND CAPTIONS: OFF")) soundCaptions = !soundCaptions;
            if (ActionButton(new Rect(374, 536, 532, 53), "RETURN TO EXAMINATION  [ESC]", true, true)) SetPaused(false);
            if (ActionButton(new Rect(374, 604, 532, 43), "RESTART EXAMINATION")) Restart();
            Text(new Rect(374, 676, 532, 43), "Progress lasts for this play session. Stop Play Mode to leave.", small, Muted);
        }

        void EndingUI()
        {
            float alpha = Mathf.Clamp01(endingTime / 2.5f);
            Fill(new Rect(-1280, -800, 3840, 2400), new Color(.035f, .045f, .041f, alpha));
            if (endingTime < 2.5f) return;
            Fill(new Rect(300, 118, 680, 555), Paper);
            Text(new Rect(346, 153, 590, 34), "DISTRICT ASSESSMENT OFFICE / RETAIN THIS COPY", small);
            Text(new Rect(342, 220, 596, 62), powerIsolated ? "NO RECORD RECEIVED" : "RELEASE PROCESSED", heading);
            string released = Session.FinalReleaseChoice == 1 ? "MARA BELL" : "CANDIDATE 014";
            string retained = Session.FinalReleaseChoice == 1 ? "CANDIDATE 014" : "MARA BELL";
            string receipt = powerIsolated ? "At 9:00, the circuit was opened.\n\nTwo records were left unsigned.\n\nThe attendance count could not be recovered." :
                "Released: " + released + "\nRetained: " + retained + "\n\nThe room is ready for its next examination.\nYour authorization remains on file.";
            Text(new Rect(346, 312, 590, 223), receipt, body);
            Text(new Rect(346, 555, 590, 55), "Original observation: " + Session.GetRecordedOptionText("clock") + "\nNo amendment requested.", small, Muted);
            if (endingTime > 5 && ActionButton(new Rect(420, 694, 440, 47), "BEGIN AGAIN", true, true)) Restart();
        }
    }
}
