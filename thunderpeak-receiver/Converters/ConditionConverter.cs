using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hl7.Fhir.Model;
using HL7.Dotnetcore;
using thunderpeak_receiver;

namespace Cdc.Surveillance.Converters
{
    public sealed class ConditionConverter
    {
        public Condition Convert(HL7.Dotnetcore.Message message, string patientId)
        {
            Condition condition = new Condition();

            var obrSegment = message.Segments("OBR")[0];
            var patientSegment = message.Segments("PID")[0];

            var illnessOnsetDate = message.Segments("OBX")
                .Where(s => s.Fields(3).Value.StartsWith("11368-8"))
                .Where(s => s.Fields(2).Value.Equals("TS"))
                .FirstOrDefault();

            var illnessEndDate = message.Segments("OBX")
                .Where(s => s.Fields(3).Value.StartsWith("77976-9"))
                .Where(s => s.Fields(2).Value.Equals("TS"))
                .FirstOrDefault();

            if (!obrSegment.Name.Equals("OBR"))
            {
                throw new InvalidOperationException($"Condition requires a valid OBR segment. Instead, received an {obrSegment.Name} segment");
            }
            if (obrSegment.GetAllFields().Count < 31)
            {
                throw new InvalidOperationException("OBR segment requires at least 31 fields");
            }

            var obr31 = obrSegment.Fields(31);

            condition.Code = new CodeableConcept(
                code: obr31.Components(1).Value, 
                display: obr31.Components(2).Value, 
                system: Common.ConvertCodeSystemString(obr31.Components(3).Value), 
                text: obr31.Components(2).Value);

            condition.Subject = new ResourceReference($"patient/{patientId}");

            condition.Category = new List<CodeableConcept>()
            {
                new CodeableConcept(
                    code: "health-concern",
                    display: "Health Concern",
                    system: "http://hl7.org/fhir/us/core/CodeSystem/condition-category",
                    text: "Health Concern")
            };

            var illnessPeriod = new Period();

            if (illnessOnsetDate != null)
            {
                illnessPeriod.Start = new FhirDateTime(illnessOnsetDate.Fields(5).Value).Value;
            }
            else if (illnessEndDate != null)
            {
                illnessPeriod.End = new FhirDateTime(illnessEndDate.Fields(5).Value).Value;
            }

            condition.Onset = illnessPeriod;

            return condition;
        }
    }
}
