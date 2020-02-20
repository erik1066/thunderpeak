using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hl7.Fhir.Model;
using HL7.Dotnetcore;
using thunderpeak_receiver;

namespace Cdc.Surveillance.Converters
{
    class SpecimenConverter
    {
        public Specimen Convert(Segment segment)
        {
            /* Converts:
             *  SPM-2
             *  SPM-4
             *  SPM-7
             *  SPM-8
             *  SPM-12
             *  SPM-18
             */

            string identifier = segment.Fields(2).Value ?? string.Empty;
            string receviedTime = segment.GetAllFields().Count >= 18 ? segment.Fields(18).Value ?? string.Empty : string.Empty;

            Specimen specimen = new Specimen()
            {
                Identifier = new List<Identifier>()
                {
                    new Identifier()
                    {
                        Value = identifier
                    }
                },
                ReceivedTimeElement = new FhirDateTime(receviedTime)
            };

            var specimenType = new CodeableConcept(
                system: Common.ConvertCodeSystemString(segment.Fields(4).Components(3).Value ?? ""),
                code: segment.Fields(4).Components(1).Value ?? "",
                display: segment.Fields(4).Components(2).Value ?? "",
                text: segment.Fields(4).Components().Count >= 9 ? segment.Fields(4).Components(9).Value : "");

            specimen.Type = specimenType;

            specimen.Collection = new Specimen.CollectionComponent()
            {
                BodySite = new CodeableConcept(
                    system: Common.ConvertCodeSystemString(segment.Fields(8).Components(3).Value ?? ""),
                    code: segment.Fields(8).Components(1).Value ?? "",
                    display: segment.Fields(8).Components(2).Value ?? "",
                    text: segment.Fields(8).Components().Count >= 9 ? segment.Fields(8).Components(9).Value : ""),
                Method = new CodeableConcept(
                    system: Common.ConvertCodeSystemString(segment.Fields(7).Components(3).Value ?? ""),
                    code: segment.Fields(7).Components(1).Value ?? "",
                    display: segment.Fields(7).Components(2).Value ?? "",
                    text: segment.Fields(7).Components().Count >= 9 ? segment.Fields(7).Components(9).Value : ""),
                Collected = new FhirDateTime(segment.GetAllFields().Count >= 17 ? segment.Fields(17).Value : "")
            };

            if (segment.Fields(12).Components().Count >= 2)
            {
                var quantity = new SimpleQuantity();
                var legacyQuantityValue = segment.Fields(12).Components(1).Value;
                if(decimal.TryParse(legacyQuantityValue, out decimal quantityValue))
                {
                    quantity.Value = quantityValue;

                    var legacyCodeValue = segment.Fields(12).Components(2).Value.Split('&');
                    if (legacyCodeValue.Length >= 3)
                    {
                        quantity.Code = legacyCodeValue[0];
                        quantity.Unit = legacyCodeValue[1];
                        quantity.System = Common.ConvertCodeSystemString(legacyCodeValue[2]);
                    }

                    specimen.Collection.Quantity = quantity;
                }
            }

            return specimen;
        }

    }
}
