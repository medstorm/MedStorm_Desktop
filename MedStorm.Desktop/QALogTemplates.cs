using DocumentFormat.OpenXml.Drawing;
using System.Collections.Generic;

namespace MedStorm.Desktop
{
    public enum QALogUseCase
    {
        PretermInfants,
        ICU,
        OperatingTheatre
    }

    public class QALogTemplate
    {
        public string Header { get; set; }
        public List<string> Interventions { get; set; }
        public List<string> InputParameters { get; set; }
        public List<string> OutcomeParameters { get; set; }
    }

    public static class QALogTemplates
    {
        public static readonly Dictionary<QALogUseCase, QALogTemplate> Templates = new()
        {
            [QALogUseCase.PretermInfants] = new QALogTemplate
            {
                Header = "QA Log: Preterm infants postoperatively in need of opioids",
                Interventions = new()
                {
                    "No interventions", "During suction from traches", "During change of bandage", "During heel stick", "During other procedural painful events", "During diaper change",
                    "Increase dose of opioids", "Increase dose of alfa2 agonists", "Increase dose of midazolam", "Decrease dose of opioids", "Decrease dose of alfa2 agonists", "Decrease dose of midazolam",
                    "Entering postoperative setting", "Leaving postoperative setting"
                },
                InputParameters = new()
                {
                    "Gestational age (weeks)", "Postnatal age (days)", "Gender (0-female) (1-male)", "Weight (gram)", "SNAP-II score (0-155)", "Finnegan (0-46)", "Observational PainScore", "PainSensor index (0-10)",
                    "Ventilation (0-no) (1-yes)", "CPAP (0-no) (1-yes)", "Oxygen saturation (%)", "FiO2 (%)", "Respiratory rate (bpm)", "Heart rate (bpm)", "Morphine (mg)", "Fentanyl (mcg)", "Midazolam (mg)", "Alfa2 agonists (mg)"
                },
                OutcomeParameters = new()
                {
                    "Wean off ventilation time (mins)", "Length of ventilation (mins)", "Length of oxygen need (mins)", "Opioid consumption per day", "Belly ache / guts moving (mins)", "Length of stay in postoperative setting (mins)", "Weight gain (gram)"
                }
            },
            [QALogUseCase.ICU] = new QALogTemplate
            {
                Header = "QA Log: Unconscious ICU-patients in need of opioids",
                Interventions = new()
                {
                    "Entering postoperative setting", "Leaving postoperative setting", "No interventions", "During suction from traches", "During change of bandage", "During stick for blood sampling", "During other procedural painful events",
                    "Increase dose of opioids", "Increase dose of alfa2 agonists", "Increase dose of midazolam", "Decrease dose of opioids", "Decrease dose of alfa2 agonists", "Decrease dose of hypnotics"
                },
                InputParameters = new()
                {
                    "Age (years)", "Gender (0-female) (1-male)", "Weight (kg)", "ASA (1-6)", "Abstinence (0-no) (1-yes)", "CPOT (0-8)", "PainSensor index (0-10)", "Ventilated (0-no) (1-yes)", "Oxygen saturation (%)",
                    "FiO2 (%)", "Respiratory rate (bpm)", "Heart rate (bpm)", "Blood pressure systolic (mmHg)", "Hypotension (0-no) (1-yes)", "Opioids (mg)", "Alfa2 agonists (mg)", "Midazolam (mg)"
                },
                OutcomeParameters = new()
                {
                    "Wean off ventilation time (hours)", "Length of ventilation (hours)", "Length of oxygen need (hours)", "Opioid consumption per day", "Delirium (0-no) (1-yes)", "Drowsiness (0-no) (1-yes)", "Nausea/Vomiting (0-no) (1-yes)", "Constipation (0-no) (1-yes)",
                    "Respiratory depression (0-no) (1-yes)", "Anxiety (0-no) (1-yes)", "Abstinence (0-no) (1-yes)", "Tolerance (0-no) (1-yes)", "Length of stay in postoperative setting (mins)"
                }
            },
            [QALogUseCase.OperatingTheatre] = new QALogTemplate
            {
                Header = "QA Log: Surgery and postoperative patients in need of opioids",
                Interventions = new()
                {
                    "Induction of anaesthesia", "No intervention sedated patient", "Max value during Intubation", "Max value during Incision", "Stop antinociceptive drugs after surgery", "Stop hypnotics after surgery",
                    "Increase dose of anti-nociceptive drugs", "Decrease dose of anti-nociceptive drugs", "Increase dose of hypnotics", "Decrease dose of hypnotics", "Max value during extubation"
                },
                InputParameters = new()
                {
                    "Age (years)", "Gender (1-male) (2-female)", "Weight (kg)", "ASA", "Spontaneous breathing (no-0) (yes-1)", "Heart rate", "Systolic Blood pressure mm hg ", "Movement (0-no) (1-yes)", "Pain Nociceptive Sensor index", "Neuro-muscular block (0-no) (1-yes)", "EEG: Suppression time (mins)"
                },
                OutcomeParameters = new()
                {
                    "Ventilation time during surgery (mins)", "Time from stop anti-nociceptive drugs to extubated (mins)", "Opioid consumption during surgery (mg)" /*same as othres*/, "Respiratory depression (0-no) (1-yes)", "Max NRS 1st postop day (0-10)", "Max PainSensor index 1. postop day",
                    "Opioid use 1. postop day (mg)", "Delirium 1. postop day (0-no) (1-yes)", "Drowsiness 1. postop day (0-no) (1-yes)", "Nausea/Vomiting 1. postop day (0-no) (1-yes)", "Constipation 1. postop day (0-no) (1-yes)",
                    "Max Anxiety 1st postop day (score 1-10)", "Abstinence 1. postop day (0-no) (1-yes)", "Length of stay in postoperative setting (mins)"
                }
            }
        };
    }
}



//namespace MedStorm.Desktop
//{
//    public enum QALogUseCase
//    {
//        PretermInfants,
//        ICU,
//        OperatingTheatre
//    }

//    public class QALogTemplate
//    {
//        public string Header { get; set; }
//        public List<string> Interventions { get; set; }
//        public List<string> InputParameters { get; set; }
//        public List<string> OutcomeParameters { get; set; }
//    }

//    public static class QALogTemplates
//    {
//        public static readonly Dictionary<QALogUseCase, QALogTemplate> Templates = new()
//        {
//            [QALogUseCase.PretermInfants] = new QALogTemplate
//            {
//                Header = "QA Log: Preterm infants postoperatively in need of opioids",
//                Interventions = new()
//                {
//                    "No interventions", "During suction from traches", "During change of bandage", "During heel stick", "During other procedural painful events", "During diaper change",
//                    "Increase dose of opioids", "Increase dose of Alfa2 agonists", "Increase dose of Midazolam", "Decrease dose of opioids", "Decrease dose of Alfa2 agonists", "Decrease dose of Midazolam",
//                    "Entering postoperative setting", "Leaving postoperative setting"
//                },
//                InputParameters = new()
//                {
//                    "Gestational age", "Postnatal age", "Gender (1-male) (2-female)", "Weight (kg)", "SNAP illness score", "Finnegan (Abstinence score)", "Observational PainScore", "PainSensor index (0-10)",
//                    "Ventilation (1-yes) (2-no)", "CPAP (1-yes) (2-no)", "Oxygen saturation", "FiO2", "Breathing (respiratory rate)", "Heart rate", "Opioids (mg)", "Alfa2 agonists (mg)", "Midazolam (mg)", "Glucose (mmol/l)"
//                },
//                OutcomeParameters = new()
//                {
//                    "Wean off ventilation time (hours)", "Length of ventilation (hours)", "Length of oxygen need (hours)", "Opioid consumption per day", "Belly ache / guts moving (hours)", "Length of stay in postoperative setting (hours)", "Weight gain (gram)"
//                }
//            },
//            [QALogUseCase.ICU] = new QALogTemplate
//            {
//                Header = "QA Log: ICU-patients in need of opioids",
//                Interventions = new()
//                {
//                    "Entering postoperative setting", "Leaving postoperative setting", "No interventions", "During suction from traches", "During change of bandage", "During stick for blood sampling", "During other procedural painful events",
//                    "Increase dose of opioids", "Increase dose of Alfa2 agonists", "Increase dose of Midazolam", "Decrease dose of opioids", "Decrease dose of Alfa2 agonists", "Decrease dose of Midazolam"
//                },
//                InputParameters = new()
//                {
//                    "Age", "Gender (1-male) (2-female)", "Weight (kg)", "ASA", "Abstinence (1-yes) (2-no)", "Observational PainScore", "PainSensor index (0-10)", "Ventilated (1-yes) (2-no)", "Oxygen saturation",
//                    "FiO2", "Breathing (respiratory rate)", "Heart rate", "Blood pressure (mean)", "Periods hypotension (1-yes) (2-no)", "Opioids (mg)", "Alfa2 agonists (mg)", "Midazolam (mg)"
//                },
//                OutcomeParameters = new()
//                {
//                    "Wean off ventilation time (hours)", "Length of ventilation (hours)", "Length of oxygen need (hours)", "Opioid consumption per day", "Delirium (1-yes) (2-no)", "Drowsiness (1-yes) (2-no)", "Nausea/Vomiting (1-yes) (2-no)", "Constipation (1-yes) (2-no)",
//                    "Respiratory depression (1-yes) (2-no)", "Anxiety (1-yes) (2-no)", "Abstinence (1-yes) (2-no)", "Tolerance (1-yes) (2-no)", "Length of stay in postoperative setting (hours)"
//                }
//            },
//            [QALogUseCase.OperatingTheatre] = new QALogTemplate
//            {
//                Header = "QA Log: Surgery and postoperative patients in need of opioids",
//                Interventions = new()
//                {
//                    "Induction of anaesthesia", "No intervention sedated patient", "Max value during Intubation", "Max value during Incision", "Stop antinociceptive drugs after surgery", "Stop hypnotics after surgery",
//                    "Increase dose of anti-nociceptive drugs", "Decrease dose of anti-nociceptive drugs", "Increase dose of hypnotics", "Decrease dose of hypnotics", "Max value during extubation"
//                },
//                InputParameters = new()
//                {
//                    "Age", "Gender (1-male) (2-female)", "Weight (kg)", "ASA", "Spontaneous breathing (yes-1) (no-2)", "Heart rate", "Systolic BT", "Movement (1-yes) (2-no)", "Pain Nociceptive Sensor index", "Neuro-muscular block (1-yes) (2-no)", "EEG: Suppression time"
//                },
//                OutcomeParameters = new()
//                {
//                    "Ventilation time during surgery (min)", "Time from stop anti-nociceptive drugs to extubated (min)", "Opioid consumption during surgery (mg)", "Respiratory depression (1-yes) (2-no)", "Max NRS 1. postop day", "Max PainSensor index 1. postop day",
//                    "Opioid use 1. postop day (mg)", "Delirium 1. postop day (1-yes) (2-no)", "Drowsiness 1. postop day (1-yes) (2-no)", "Nausea/Vomiting 1. postop day (1-yes) (2-no)", "Constipation 1. postop day (1-yes) (2-no)",
//                    "Max Anxiety 1. postop day (score 1-10)", "Abstinence 1. postop day (1-yes) (2-no)", "Length of stay in postoperative setting (hours)"
//                }
//            }
//        };
//    }
//}


