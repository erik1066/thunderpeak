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
    public class CaseFhirConverter : IFhirConverter<Case>
    {
        /// <summary>
        /// Converts a CDC case notification message from HL7 v2.5.1 ORU_R01 to a FHIR (R4) eICR
        /// </summary>
        /// <param name="rawMessage">The HL7v2 message</param>
        /// <returns>FHIR resource representing a CDC initial case notification</returns>
        public Case Convert(string rawMessage, string processId)
        {
            Message message = new Message(rawMessage);
            message.ParseMessage();

            var caseNotification = ConvertCase(message);
            var patient = ConvertPatient(message);

            caseNotification.Patient = patient;

            return caseNotification;
        }

        /// <summary>
        /// Converts specific parts of the HL7v2 message into a FHIR resource representing a CDC case notification
        /// </summary>
        /// <param name="message">The HL7v2 message</param>
        /// <returns>FHIR resource representing a CDC initial case notification</returns>
        private Case ConvertCase(Message message)
        {
            var caseId = message.Segments("OBR")[0].Fields(3).Value;

            var caseNotification = new Case()
            {
                Active = true,
                Text = new Narrative(),
            };

            #region Identifiers
            var caseIdentifier = new Identifier()
            {
                Use = Identifier.IdentifierUse.Official,
                Value = message.Segments("OBR")[0].Fields(3).Components(1).Value
            };
            caseNotification.Identifier.Add(caseIdentifier);

            var v2legacyIdentifier = GetOBXFieldValueFromV2Message(message, "77997-5");

            if (!string.IsNullOrWhiteSpace(v2legacyIdentifier.Code))
            {
                var legacyIdentifier = new Identifier()
                {
                    Use = Identifier.IdentifierUse.Old,
                    Value = v2legacyIdentifier.Code
                };
                caseNotification.Identifier.Add(legacyIdentifier);
            }
            #endregion

            #region Exposure addresses

            var v2ExposureAddresses = GetOBXRepeatingFieldValuesFromV2Message(message, new List<string>() { "77985-0", "77984-3", "77986-8", "77987-6" });

            foreach (var v2AddressGroup in v2ExposureAddresses)
            {
                var exposureAddress = new Address();

                var v2ExposureState = v2AddressGroup.Value.Where(s => s.Obx3.Equals("77985-0")).FirstOrDefault();
                var v2ExposureCountry = v2AddressGroup.Value.Where(s => s.Obx3.Equals("77984-3")).FirstOrDefault();
                var v2ExposureCity = v2AddressGroup.Value.Where(s => s.Obx3.Equals("77986-8")).FirstOrDefault();
                var v2ExposureDistrict = v2AddressGroup.Value.Where(s => s.Obx3.Equals("77987-6")).FirstOrDefault();

                exposureAddress.State = v2ExposureState?.Code;
                exposureAddress.Country = v2ExposureCountry?.Code;
                exposureAddress.City = v2ExposureCity?.Code;
                exposureAddress.District = v2ExposureDistrict?.Code;
                caseNotification.ExposureAddress.Add(exposureAddress);
            }
            #endregion

            #region Transmission mode
            var v2transmissionMode = GetOBXFieldValueFromV2Message(message, "77989-2");
            var transmissionMode = new CodeableConcept(
                code: v2transmissionMode.Code,
                text: v2transmissionMode.Text,
                system: ConvertCodeSystemString(v2transmissionMode.System));
            caseNotification.TransmissionMode = transmissionMode;
            #endregion

            #region Outbreak association
            var v2outbreak = GetOBXFieldValueFromV2Message(message, "77981-9").Code;
            if (!string.IsNullOrWhiteSpace(v2outbreak))
            {
                caseNotification.Outbreak = new FhirString(v2outbreak);
            }
            #endregion

            #region Result status
            var v2resultStatus = message.Segments("OBR")[0].Fields(25).Value;
            var resultStatus = new FhirString(v2resultStatus);
            caseNotification.ResultStatus = resultStatus;
            #endregion

            #region Imported condition information
            var v2importedIndicator = GetOBXFieldValueFromV2Message(message, "77982-7");
            var importedIndicator = new CodeableConcept(
                code: v2importedIndicator.Code,
                text: v2importedIndicator.Text,
                system: ConvertCodeSystemString(v2importedIndicator.System));
            caseNotification.ImportedIndicator = importedIndicator;

            var importedAddress = new Address();
            importedAddress.Country = GetOBXFieldValueFromV2Message(message, "INV153").Code;
            importedAddress.State = GetOBXFieldValueFromV2Message(message, "INV154").Code;
            importedAddress.City = GetOBXFieldValueFromV2Message(message, "INV155").Code;
            importedAddress.District = GetOBXFieldValueFromV2Message(message, "INV156").Code;
            caseNotification.ImportedAddress.Add(importedAddress);
            #endregion

            #region Binational reporting criteria
            var v2binational = GetOBXFieldValueFromV2Message(message, "77988-4");
            var multinational = new CodeableConcept(
                code: v2binational.Code,
                text: v2binational.Text,
                system: ConvertCodeSystemString(v2binational.System));
            caseNotification.MultinationalReportingCriteria.Add(multinational);
            #endregion

            return caseNotification;
        }
        private string ConvertCodeSystemString(string system)
        {
            switch (system)
            {
                case "SCT":
                    return "http://snomed.info/sct";
                default:
                    return system;
            }
        }

        private ObxTriplet GetOBXFieldValueFromV2Message(Message message, string obx3)
        {
            Segment obxSegment = message.Segments("OBX")
                .Where(f => f.Fields(3).Components(1).Value.Equals(obx3))
                .Where(f => !string.IsNullOrWhiteSpace(f.Fields(5).Value))
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

        private Patient ConvertPatient(Message message)
        {
            var patient = new Patient()
            {
                Active = true,
                Text = new Narrative(),
            };

            var patientIdentifier = new Identifier();
            patientIdentifier.Value = System.Guid.NewGuid().ToString();
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
    }
}