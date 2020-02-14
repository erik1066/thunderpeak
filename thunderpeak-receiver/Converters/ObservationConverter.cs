﻿using System;
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
    public class ObservationConverter
    {
        public Observation Convert(Segment segment)
        {   
            var obx3codeSystem = string.Empty;
            if (segment.Fields(3).Components().Count >= 3)
            {
                obx3codeSystem = segment.Fields(3).Components(3).Value;
            }

            Observation observation = new Observation()
            {
                Code = new CodeableConcept()
                {
                    Coding = new List<Coding>()
                    {
                        new Coding
                        {
                            Code = segment.Fields(3).Components(1).Value,
                            Display = segment.Fields(3).Components(2).Value,
                            System = thunderpeak_receiver.Common.ConvertCodeSystemString(obx3codeSystem),
                            Extension = new List<Extension>() {
                                new Extension()
                                {
                                    Value = new FhirString(obx3codeSystem)
                                }
                            }
                        }
                    }
                },
                Subject = new ResourceReference("TODO: Create reference to the patient here"),
                Identifier = new List<Identifier>()
                {
                    new Identifier()
                    {
                        Value = segment.Fields(3).Components(1).Value
                    }
                }
            };

            if (segment.GetAllFields().Count >= 20 && !string.IsNullOrWhiteSpace(segment.Fields(20).Value) && segment.Fields(20).Components().Count >= 3)
            {
                var bodySite = segment.Fields(20);
                // Map to BodySite
                observation.BodySite = new CodeableConcept(bodySite.Components(3).Value, bodySite.Components(1).Value, bodySite.Components(2).Value, string.Empty);
            }
            if (segment.GetAllFields().Count >= 17 && !string.IsNullOrWhiteSpace(segment.Fields(17).Value) && segment.Fields(17).Components().Count >= 3)
            {
                var method = segment.Fields(17);
                // Map to Method
                observation.Method = new CodeableConcept(method.Components(3).Value, method.Components(1).Value, method.Components(2).Value, string.Empty);
            }
            if (segment.GetAllFields().Count >= 11 && !string.IsNullOrWhiteSpace(segment.Fields(11).Value))
            {
                var status = segment.Fields(11).Value;
                // Map to Status
                observation.Status = status switch
                {
                    "A" => ObservationStatus.Amended,
                    "C" => ObservationStatus.Corrected,
                    "F" => ObservationStatus.Final,
                    "P" => ObservationStatus.Preliminary,
                    "W" => ObservationStatus.EnteredInError,
                    _ => ObservationStatus.Unknown,
                };
            }
            if (segment.GetAllFields().Count >= 8 && !string.IsNullOrWhiteSpace(segment.Fields(8).Value))
            {
                Field interpretationField = segment.Fields(8);

                if (interpretationField.Components().Count >= 3)
                {
                    var interpCode = interpretationField.Components(1).Value;
                    var interpName = interpretationField.Components(2).Value;
                    var interpSys = interpretationField.Components(3).Value;

                    var interpText = interpretationField.Components().Count == 9 ? interpretationField.Components(9).Value : string.Empty;

                    observation.Interpretation.Add(new CodeableConcept(interpSys, interpCode, interpName, interpText));
                }
            }

            if (segment.GetAllFields().Count >= 7 && !string.IsNullOrWhiteSpace(segment.Fields(7).Value))
            {
                Field referenceRangeField = segment.Fields(7);
                string referenceRange = referenceRangeField.Value;

                if (!string.IsNullOrWhiteSpace(referenceRange))
                {
                    observation.ReferenceRange.Add(new Observation.ReferenceRangeComponent()
                    {
                        Text = referenceRange
                    });
                }
            }

            if (segment.GetAllFields().Count >= 14 && !string.IsNullOrWhiteSpace(segment.Fields(14).Value))
            {
                string specimenCollectionDate = segment.Fields(14).Value;
                observation.Effective = new FhirDateTime(specimenCollectionDate);
            }

            var hl7v2dataType = segment.Fields(2).Value;
            var obx5 = segment.Fields(5);
            bool hasUnits = segment.Fields(6).Value.Length > 0 && !string.IsNullOrWhiteSpace(segment.Fields(6).Value);

            string unitCode = hasUnits && segment.Fields(6).Components().Count >= 3 && segment.Fields(6).Components(1).Value.Length > 0 ? segment.Fields(6).Components(1).Value.Trim() : string.Empty;
            string unitName = hasUnits && segment.Fields(6).Components().Count >= 3 && segment.Fields(6).Components(2).Value.Length > 0 ? segment.Fields(6).Components(2).Value.Trim() : string.Empty;
            string unitSys = hasUnits && segment.Fields(6).Components().Count >= 3 && segment.Fields(6).Components(3).Value.Length > 0 ? segment.Fields(6).Components(3).Value.Trim() : string.Empty;

            switch (hl7v2dataType)
            {
                case "NM":
                    if (decimal.TryParse(obx5.Value, out decimal value))
                    {
                        if (hasUnits)
                        {
                            var quantity = new Quantity()
                            {
                                Value = value,
                                Code = unitCode,
                                Unit = unitName,
                                System = thunderpeak_receiver.Common.ConvertCodeSystemString(unitSys)
                            };
                            observation.Value = quantity;
                        }
                        else
                        {
                            observation.Value = new FhirDecimal(value);
                        }
                    }
                    else
                    {
                        observation.Value = new FhirString(obx5.Value);
                        // TODO: WARNING
                    }
                    break;
                case "SN":
                    if (obx5.Components().Count == 2 && obx5.Components(1).Value.Length == 0 && obx5.Components(2).Value.Length > 0)
                    {
                        if (decimal.TryParse(obx5.Components(2).Value, out decimal c2Value))
                        {
                            if (hasUnits)
                            {
                                var quantity = new Quantity()
                                {
                                    Value = c2Value,
                                    Code = unitCode,
                                    Unit = unitName,
                                    System = thunderpeak_receiver.Common.ConvertCodeSystemString(unitSys)
                                };
                                observation.Value = quantity;
                            }
                            else
                            {
                                observation.Value = new FhirDecimal(c2Value);
                            }
                        }
                    }
                    break;
                case "DT":
                case "TS":
                    observation.Value = new FhirDateTime(obx5.Value);
                    break;
                case "CE":
                case "CWE":
                    string obx5codeSystem = string.Empty;
                    if (obx5.Components().Count >= 3)
                    {
                        obx5codeSystem = obx5.Components(3).Value;
                    }

                    if (obx5.Components().Count >= 2)
                    {

                        var codeableConcept = new CodeableConcept()
                        {
                            Coding = new List<Coding>()
                                {
                                    new Coding
                                    {
                                        Code = obx5.Components(1).Value,
                                        Display = obx5.Components(2).Value,
                                        Extension = new List<Extension>()
                                        {
                                            new Extension()
                                            {
                                                Value = new FhirString(obx5codeSystem)
                                            }
                                        }
                                    }
                                }
                        };

                        if (obx5.Components().Count >= 9 && obx5.Components(9).Value.Length > 0)
                        {
                            // OBX 5.9 value
                            codeableConcept.Coding[0].Extension.Add(new Extension()
                            {
                                Value = new FhirString(obx5.Components(9).Value)
                            });
                        }

                        observation.Value = codeableConcept;
                    }
                    break;
                case "EI":
                case "ST":
                case "TX":
                case "FT":
                    observation.Value = new FhirString(segment.Fields(5).Value);
                    break;
                default:
                    // TODO: WARNING, ERROR?
                    break;
            }
            
            return observation;
        }

    }
}
