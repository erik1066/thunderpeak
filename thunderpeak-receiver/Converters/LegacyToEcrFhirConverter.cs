using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hl7.Fhir.Model;
using HL7.Dotnetcore;

namespace Cdc.Surveillance.Converters
{
    /// <summary>
    /// Class for converting a CDC case notification message from the legacy HL7 v2.5.1 ORU_R01 format 
    /// to a FHIR (R4) electronic initial case report (eICR) resource
    /// </summary>
    public class EcrFhirConverter : IFhirConverter<Bundle>
    {
        private readonly List<string> _obxIdsToExclude = new List<string>() { "78746-5", "21842-0", };

        /// <summary>
        /// Converts a CDC case notification message from HL7 v2.5.1 ORU_R01 to a FHIR (R4) eICR
        /// </summary>
        /// <param name="rawMessage">The HL7v2 message</param>
        /// <returns>FHIR resource representing a CDC initial case notification</returns>
        public Bundle Convert(string rawMessage, string processId)
        {
            Message message = new Message(rawMessage); // HL7v2 message
            message.ParseMessage();

            var bundle = ConvertEcr(message, processId);

            return bundle;
        }

        /// <summary>
        /// Converts specific parts of the HL7v2 message into a FHIR resource representing a CDC case notification
        /// </summary>
        /// <param name="message">The HL7v2 message</param>
        /// <returns>FHIR resource representing a CDC initial case notification</returns>
        private Bundle ConvertEcr(Message message, string processId)
        {
            var caseId = message.Segments("OBR")[0].Fields(3).Value;

            var bundle = new Bundle()
            {
                Id = processId,
                Type = Bundle.BundleType.Document
            };

            string patientIdentifier = System.Guid.NewGuid().ToString();
            string encounterIdentifier = System.Guid.NewGuid().ToString();

            // add the eICR composition entry as the top element
            {
                var entry1 = new Bundle.EntryComponent();

                var documentIdentifier = new Identifier()
                {
                    Use = Identifier.IdentifierUse.Official,
                    Value = processId
                };

                var composition = new Composition()
                {
                    Identifier = documentIdentifier,
                    Status = CompositionStatus.Final,
                    Title = "N/A",
                    Subject = new ResourceReference(patientIdentifier),
                    Encounter = new ResourceReference(encounterIdentifier)
                };

                var observationSection = new Composition.SectionComponent();
                observationSection.Text = new Narrative();

                var observations = ConvertObservations(message);

                foreach (var observation in observations)
                {
                    observationSection.Entry.Add(new ResourceReference("TODO: Add ref to " + observation.Code.Coding.FirstOrDefault().Code));

                    var observationEntry = new Bundle.EntryComponent();
                    observationEntry.Resource = observation;
                    bundle.Entry.Add(observationEntry);
                }
                composition.Section.Add(observationSection);

                entry1.Resource = composition;
                bundle.Entry.Add(entry1);

            }

            

            // add the patient resource
            {
                var entry2 = new Bundle.EntryComponent();

                var patient = ConvertPatient(message, patientIdentifier);
                entry2.Resource = patient;
                bundle.Entry.Add(entry2);
            }
            return bundle;
        }

        private List<Observation> ConvertObservations(Message message)
        {
            var segments = message.Segments();

            List<Observation> observations = new List<Observation>();

            foreach (var segment in segments)
            {
                if (segment.Name.Equals("OBX", StringComparison.OrdinalIgnoreCase) && _obxIdsToExclude.Contains(segment.Fields(3).Value))
                {
                    // don't turn certain OBX's into Observation resources
                    continue;
                }
                else if (segment.Name.Equals("OBX", StringComparison.OrdinalIgnoreCase))
                {
                    var obsConverter = new ObservationConverter();
                    var observation = obsConverter.Convert(segment);
                    observations.Add(observation);
                }
            }

            return observations;
        }

        private ObxTriplet GetOBXFieldValueFromV2Message(Message message, string obx3)
        {
            Segment obxSegment = message.Segments("OBX")
                .Where(s => s.Fields(3).Components(1).Value.Equals(obx3))
                .Where(s => !string.IsNullOrWhiteSpace(s.Fields(5).Value))
                .FirstOrDefault();

            if (obxSegment == null)
            {
                return new ObxTriplet()
                {
                    Obx3 = obx3
                };
            }

            switch (obxSegment.Fields(2).Value)
            {
                case "CE":
                case "CWE":
                    return new ObxTriplet(obxSegment.Fields(5).Components(1).Value, obxSegment.Fields(5).Components(2).Value, obxSegment.Fields(5).Components(3).Value, obx3);
                case "SN":
                    return new ObxTriplet(obxSegment.Fields(5).Components(2).Value, obxSegment.Fields(5).Components(2).Value, obxSegment.Fields(5).Components(2).Value, obx3);
                default:
                    return new ObxTriplet(obxSegment.Fields(5).Value, obxSegment.Fields(5).Value, obxSegment.Fields(5).Value, obx3);
            }
        }

        [DebuggerDisplay("{Obx3} = {Code} : {Text}")] // see https://docs.microsoft.com/en-us/dotnet/framework/debug-trace-profile/enhancing-debugging-with-the-debugger-display-attributes for how to use DebuggerDisplay
        private class ObxTriplet
        {
            public ObxTriplet() { }

            public ObxTriplet(string code, string text, string system, string obx3)
            {
                Code = code;
                Text = text;
                System = system;
                Obx3 = obx3;
            }

            public string Obx3 { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public string System { get; set; } = string.Empty;
        }

        private Dictionary<int, List<ObxTriplet>> GetOBXRepeatingFieldValuesFromV2Message(Message message, List<string> obx3s)
        {
            Dictionary<int, List<ObxTriplet>> repeatingValues = new Dictionary<int, List<ObxTriplet>>();

            foreach (var obx3 in obx3s)
            {
                var segmentKvps = message.Segments("OBX")
                    .Where(f => f.Fields(3).Components(1).Value.Equals(obx3))
                    .Where(f => !string.IsNullOrWhiteSpace(f.Fields(5).Value))
                    .GroupBy(f => f.Fields(4).Value);

                foreach (var kvp in segmentKvps)
                {
                    int key = int.Parse(kvp.Key);
                    Segment obxSegment = kvp.FirstOrDefault();

                    if (obxSegment == null)
                    {
                        continue;
                    }

                    var triplet = new ObxTriplet();
                    triplet.Obx3 = obx3;

                    switch (obxSegment.Fields(2).Value)
                    {
                        case "CE":
                        case "CWE":
                            triplet.Code = obxSegment.Fields(5).Components(1).Value;
                            triplet.Text = obxSegment.Fields(5).Components(2).Value;
                            triplet.System = obxSegment.Fields(5).Components(3).Value;
                            break;
                        case "SN":
                            triplet.Code = obxSegment.Fields(5).Components(2).Value;
                            triplet.Text = obxSegment.Fields(5).Components(2).Value;
                            triplet.System = obxSegment.Fields(5).Components(2).Value;
                            break;
                        default:
                            triplet.Code = obxSegment.Fields(5).Components(1).Value;
                            triplet.Text = obxSegment.Fields(5).Components(1).Value;
                            triplet.System = obxSegment.Fields(5).Components(1).Value;
                            break;
                    }

                    if (repeatingValues.ContainsKey(key))
                    {
                        repeatingValues[key].Add(triplet);
                    }
                    else
                    {
                        repeatingValues.Add(key, new List<ObxTriplet>() { triplet });
                    }
                }
            }

            return repeatingValues;
        }

        private Patient ConvertPatient(Message message, string identifier)
        {
            var patient = new Patient()
            {
                Active = true,
                Text = new Narrative(),
            };

            var patientIdentifier = new Identifier();
            patientIdentifier.Value = identifier;
            patient.Identifier.Add(patientIdentifier);

            var address = new Address();
            address.State = message.Segments("PID")[0].Fields(11).Components(4).Value;
            address.City = message.Segments("PID")[0].Fields(11).Components(3).Value;
            address.PostalCode = message.Segments("PID")[0].Fields(11).Components(5).Value;
            address.Country = message.Segments("PID")[0].Fields(11).Components(6).Value;
            address.District = message.Segments("PID")[0].Fields(11).Components(9).Value;
            patient.Address.Add(address);

            var v2gender = message.Segments("PID")[0].Fields(8).Value;

            var res = v2gender switch
            {
                "M" => patient.Gender = AdministrativeGender.Male,
                "F" => patient.Gender = AdministrativeGender.Female,
                "U" => patient.Gender = AdministrativeGender.Unknown,
                "O" => patient.Gender = AdministrativeGender.Other,
                _ => patient.Gender = null
            };

            #region Race Extension - us-core-race
            var race = ConvertRace(message);
            patient.Extension.Add(race);
            #endregion // Race Extension - us-core-race

            #region Ethnic Group Extension - us-core-ethnicity
            var ethnicGroup = ConvertEthnicity(message);
            patient.Extension.Add(ethnicGroup);
            #endregion // Ethnic Group Extension - us-core-ethnicity

            #region BirthPlace extension
            var birthPlace = ConvertBirthPlace(message);
            patient.Extension.Add(birthPlace);
            #endregion

            patient.BirthDateElement = ConvertDate(message.Segments("PID")[0].Fields(7).Value);

            patient.Deceased = ConvertDateTime(message.Segments("PID")[0].Fields(29).Value);

            Redact(patient);

            return patient;
        }

        /// <summary>
        /// Converts an HL7v2 DateTime string to a FHIR DateTime
        /// </summary>
        /// <param name="dateTime">HL7v2 dateTime or timeStamp string</param>
        /// <returns>FHIR-compliant DateTime</returns>
        private FhirDateTime ConvertDateTime(string dateTime)
        {
            if (dateTime.Length < 8)
            {
                return new FhirDateTime(); // not a valid dateTime, so don't attempt to convert
            }

            string yearStr = dateTime.Substring(0, 4);
            string monthStr = dateTime.Substring(4, 2);
            string dayStr = dateTime.Substring(6, 2);

            string hourStr = dateTime.Length > 8 ? dateTime.Substring(8, 2) : "00";
            string minStr = dateTime.Length > 10 ? dateTime.Substring(10, 2) : "00";
            string secStr = dateTime.Length > 12 ? dateTime.Substring(12, 2) : "00";

            int.TryParse(yearStr, out int year);
            int.TryParse(monthStr, out int month);
            int.TryParse(dayStr, out int day);
            int.TryParse(hourStr, out int hour);
            int.TryParse(minStr, out int minute);
            int.TryParse(secStr, out int second);

            FhirDateTime fhirDateTime = new FhirDateTime(year, month, day, hour, minute, second, new TimeSpan());
            return fhirDateTime;
        }

        /// <summary>
        /// Converts an HL7v2 DateTime string to a FHIR Date
        /// </summary>
        /// <param name="dateTime">HL7v2 dateTime or timeStamp string</param>
        /// <returns>FHIR-compliant Date</returns>
        private Date ConvertDate(string dateTime)
        {
            if (dateTime.Length < 8)
            {
                return new Date(); // not a valid dateTime, so don't attempt to convert
            }

            string yearStr = dateTime.Substring(0, 4);
            string monthStr = dateTime.Substring(4, 2);
            string dayStr = dateTime.Substring(6, 2);

            int.TryParse(yearStr, out int year);
            int.TryParse(monthStr, out int month);
            int.TryParse(dayStr, out int day);

            Date fhirDate = new Date(year, month, day);
            return fhirDate;
        }

        private void Redact(Patient patient)
        {
            patient.Name.Clear();
            patient.Telecom.Clear();
            patient.Contact.Clear();
            patient.Photo.Clear();
        }

        private Extension ConvertEthnicity(Message message)
        {
            var ethnicGroup = new Extension();
            ethnicGroup.Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity";

            var ethnicGroupFields = new List<Field>();

            if (message.Segments("PID")[0].Fields(22).HasRepetitions)
            {
                ethnicGroupFields.AddRange(message.Segments("PID")[0].Fields(22).Repetitions());
            }
            else
            {
                ethnicGroupFields.Add(message.Segments("PID")[0].Fields(22));
            }

            foreach (var v2ethnicGroup in ethnicGroupFields)
            {
                var ombCategory = new Extension()
                {
                    Url = "ombCategory",
                    Value = new Coding(
                        "urn:oid:2.16.840.1.113883.6.238",
                        v2ethnicGroup.Components(1).Value,
                        v2ethnicGroup.Components(2).Value),
                };
                ethnicGroup.Extension.Add(ombCategory);
            }

            return ethnicGroup;
        }

        private Extension ConvertRace(Message message)
        {
            var race = new Extension();
            race.Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race";

            var raceFields = new List<Field>();

            if (message.Segments("PID")[0].Fields(10).HasRepetitions)
            {
                raceFields.AddRange(message.Segments("PID")[0].Fields(10).Repetitions());
            }
            else
            {
                raceFields.Add(message.Segments("PID")[0].Fields(10));
            }

            foreach (var v2race in raceFields)
            {
                var ombCategory = new Extension()
                {
                    Url = "ombCategory",
                    Value = new Coding("urn:oid:2.16.840.1.113883.6.238", v2race.Components(1).Value, v2race.Components(2).Value),
                };
                race.Extension.Add(ombCategory);
            }

            return race;
        }

        private Extension ConvertBirthPlace(Message message)
        {
            var birthCountry = GetOBXFieldValueFromV2Message(message, "78746-5").Text;
            var birthCountryOther = GetOBXFieldValueFromV2Message(message, "21842-0").Text;

            var birthPlace = new Extension();
            birthPlace.Url = "http://hl7.org/fhir/StructureDefinition/patient-birthPlace";
            birthPlace.Value = new Address()
            {
                Country = birthCountry,
                Text = birthCountryOther
            };

            return birthPlace;
        }
    }
}