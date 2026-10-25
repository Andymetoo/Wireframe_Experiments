using UnityEngine;

namespace TestAnxiety
{
    public sealed partial class TestAnxietyGame
    {
        string ReportInstruction(ExamResult result)
        {
            string id = Session.Sections[result.SectionIndex].Id;
            if (id == "calibration") return "Retain this copy. It is your record of what you saw.";
            if (id == "verification") return "RECORD DISCREPANCY. A retained copy is waiting in Records. Acknowledge to release the entrance.";
            if (id == "allocation") return "The available space has been entered into the inventory.";
            if (id == "retention") return "Carbon transfers are admissible. The reader's record takes precedence.";
            if (id == "release") return Session.FinalReleaseChoice >= 2 ? "MANUAL RELEASE. Isolate reader power at the entrance switch; use the outer door in Records." : "Release issued. Use the outer door in Records.";
            return "Retain this record for comparison.";
        }

        void ArchiveUI()
        {
            PaperFrame("RETAINED ATTENDANCE RECORD", "CARBON TRANSFER / DUPLICATE");
            Text(new Rect(80, 153, 650, 30), "NORTH COUNTY SCHOOL DISTRICT  /  ROOM 107", small, Muted);
            Text(new Rect(80, 207, 650, 48), "MARA BELL", heading);
            Text(new Rect(83, 264, 635, 122), "Assigned workstation: 06\nAttendance classification: EXCUSED\nReason: Awaiting collection\nLast recorded departure: —", body);
            Fill(new Rect(81, 397, 650, 1), Muted);
            Text(new Rect(83, 417, 635, 151), "Please do not clear her desk.\nHer mother said she would come after school.\n\nThe call was logged at 3:15.\nThe line is still open.", body);
            Text(new Rect(83, 596, 635, 64), "Do not mark an absent candidate as present.\nDo not leave a present candidate without a record.", small, Red);
            Text(new Rect(800, 151, 380, 49), "The following marks were transferred from the original assessment.", small, Muted);
            string[] ids = { "clock", "windows", "notice" };
            string[] labels = { "CLOCK", "WINDOWS", "EXAMINATION NOTICE" };
            for (int i = 0; i < ids.Length; i++)
            {
                float y = 219 + i * 91;
                Text(new Rect(803, y, 375, 28), labels[i], tiny, Muted);
                string answer = Session.GetRecordedOptionText(ids[i]);
                Text(new Rect(803, y + 30, 375, 45), answer.Length == 0 ? "NO RESPONSE RECORDED" : answer, heading);
            }
            Fill(new Rect(802, 506, 380, 1), Muted);
            Text(new Rect(803, 525, 375, 115), "Requests at classroom door: " + doorAttempts + "\nPencil corrections: " + Session.SelectionEdits + "\n\nExaminer signature: 014", small);
            Text(new Rect(803, 646, 375, 34), "CARBON COPY — DO NOT ERASE", tiny, Red);
            if (Event.current.type == EventType.Repaint && Session.NeedsArchiveRecord) Session.MarkArchiveRead();
            if (ActionButton(new Rect(80, 693, 650, 44), "LOWER RECORD  [E / ESC]")) CloseInspection();
            if (Session.NeedsArchiveRecord)
            {
                if (ActionButton(new Rect(800, 693, 384, 44), "TAKE RETAINED COPY", Session.ArchiveRead)) CollectRecord();
            }
            else if (ActionButton(new Rect(800, 693, 384, 44), "KEEP WITH YOUR PAPERS")) CloseInspection();
        }

        void DocumentUI()
        {
            Fill(new Rect(285, 70, 725, 665), new Color(.02f, .025f, .02f, .5f));
            Fill(new Rect(270, 56, 725, 665), Paper);
            Text(new Rect(316, 94, 632, 30), "NORTH COUNTY SCHOOL DISTRICT", small, Muted);
            Text(new Rect(312, 154, 640, 85), Room.DocumentTitles[documentIndex], heading);
            Fill(new Rect(316, 254, 632, 1), Muted);
            string content = Room.DocumentBodies[documentIndex];
            float height = Mathf.Max(334, body.CalcHeight(new GUIContent(content), 606) + 8);
            documentScroll = GUI.BeginScrollView(new Rect(316, 280, 632, 338), documentScroll, new Rect(0, 0, 606, height));
            Text(new Rect(0, 0, 606, height), content, body);
            GUI.EndScrollView();
            if (ActionButton(new Rect(316, 641, 632, 43), "PUT DOWN  [E / ESC]")) CloseInspection();
        }

        void SeatedUI()
        {
            if (elapsed < messageUntil)
            {
                Fill(new Rect(207, 711, 866, 62), new Color(.055f, .085f, .075f, .88f));
                Text(new Rect(225, 717, 830, 53), message, centered, White);
            }
            else Text(new Rect(424, 737, 432, 30), "MOUSE  LOOK     E / ESC  STAND", centered, White);
            if (elapsed < hintUntil)
            {
                Fill(new Rect(292, 32, 696, 67), new Color(.06f, .09f, .08f, .82f));
                Text(new Rect(311, 41, 658, 51), Session.NeedsQuietInterval ? "The interval is in progress. If you stand, it resumes when you sit again." : "Your paper is being returned. E to stand and collect it.", small, White);
            }
        }

        void SequenceOverlayUI()
        {
            if (Session.Stage == ExamStage.Complete) return;
            float obscuration = finaleActive ? FinaleFade() : BlackoutAlpha();
            if (obscuration > 0) Fill(new Rect(-1280, -800, 3840, 2400), new Color(.008f, .012f, .01f, obscuration));
            if (soundCaptions && elapsed < captionUntil && caption.Length > 0)
            {
                Fill(new Rect(318, 604, 644, 42), new Color(.035f, .05f, .045f, .88f));
                Text(new Rect(331, 609, 618, 32), caption, centered, White);
            }
        }
    }
}
