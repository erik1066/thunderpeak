using System;
using System.Collections.Generic;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Validation;
using System.Linq;
using System.Runtime.Serialization;
using Hl7.Fhir.Utility;

#pragma warning disable 1591 // suppress XML summary warnings 

namespace Hl7.Fhir.Model
{
    /// <summary>
    /// Information about a case
    /// </summary>
    [FhirType("Case", IsResource = true)]
    [DataContract]
    public partial class Case : Hl7.Fhir.Model.DomainResource, System.ComponentModel.INotifyPropertyChanged
    {
        [NotMapped]
        public override ResourceType ResourceType { get { return ResourceType.Bundle; } }
        [NotMapped]
        public override string TypeName { get { return "Case"; } }

        /// <summary>
        /// An identifier for this case
        /// </summary>
        [FhirElement("identifier", InSummary = true, Order = 90)]
        [Cardinality(Min = 0, Max = -1)]
        [DataMember]
        public List<Hl7.Fhir.Model.Identifier> Identifier
        {
            get { if (_Identifier == null) _Identifier = new List<Hl7.Fhir.Model.Identifier>(); return _Identifier; }
            set { _Identifier = value; OnPropertyChanged("Identifier"); }
        }
        private List<Hl7.Fhir.Model.Identifier> _Identifier;

        /// <summary>
        /// Whether this case's record is in active use
        /// </summary>
        [FhirElement("active", InSummary = true, Order = 100)]
        [DataMember]
        public Hl7.Fhir.Model.FhirBoolean ActiveElement
        {
            get { return _ActiveElement; }
            set { _ActiveElement = value; OnPropertyChanged("ActiveElement"); }
        }
        private Hl7.Fhir.Model.FhirBoolean _ActiveElement;

        /// <summary>
        /// Whether this case's record is in active use
        /// </summary>
        /// <remarks>This uses the native .NET datatype, rather than the FHIR equivalent</remarks>
        [NotMapped]
        [IgnoreDataMemberAttribute]
        public bool? Active
        {
            get { return ActiveElement != null ? ActiveElement.Value : null; }
            set
            {
                if (!value.HasValue)
                    ActiveElement = null;
                else
                    ActiveElement = new Hl7.Fhir.Model.FhirBoolean(value);
                OnPropertyChanged("Active");
            }
        }

        /// <summary>
        /// The patient information for this case
        /// </summary>
        [FhirElement("patient", InSummary = true, Order = 140)]
        [DataMember]
        public Hl7.Fhir.Model.Patient Patient
        {
            get { if (_Patient == null) _Patient = new Hl7.Fhir.Model.Patient(); return _Patient; }
            set { _Patient = value; OnPropertyChanged("Patient"); }
        }
        private Hl7.Fhir.Model.Patient _Patient;

        /// <summary>
        /// The address where the patient was exposed
        /// </summary>
        [FhirElement("exposureAddress", InSummary = true, Order = 160)]
        [Cardinality(Min = 0, Max = -1)]
        [DataMember]
        public List<Hl7.Fhir.Model.Address> ExposureAddress
        {
            get { if (_ExposureAddress == null) _ExposureAddress = new List<Hl7.Fhir.Model.Address>(); return _ExposureAddress; }
            set { _ExposureAddress = value; OnPropertyChanged("ExposureAddress"); }
        }
        private List<Hl7.Fhir.Model.Address> _ExposureAddress;

        /// <summary>
        /// The mode of transmission for the disease (if applicable)
        /// </summary>
        [FhirElement("transmissionMode", InSummary = true, Order = 170)]
        [DataMember]
        public Hl7.Fhir.Model.CodeableConcept TransmissionMode
        {
            get { if (_TransmissionMode == null) _TransmissionMode = new Hl7.Fhir.Model.CodeableConcept(); return _TransmissionMode; }
            set { _TransmissionMode = value; OnPropertyChanged("TransmissionMode"); }
        }
        private Hl7.Fhir.Model.CodeableConcept _TransmissionMode;

        /// <summary>
        /// Whether this case meets the criteria for immediate notification to the CDC
        /// </summary>
        [FhirElement("immediateNotification", InSummary = true, Order = 180)]
        [DataMember]
        public Hl7.Fhir.Model.CodeableConcept ImmediateNotification
        {
            get { if (_ImmediateNotification == null) _ImmediateNotification = new Hl7.Fhir.Model.CodeableConcept(); return _ImmediateNotification; }
            set { _ImmediateNotification = value; OnPropertyChanged("ImmediateNotification"); }
        }
        private Hl7.Fhir.Model.CodeableConcept _ImmediateNotification;

        /// <summary>
        /// The name of any associated outbreak this case belongs to
        /// </summary>
        [FhirElement("outbreak", InSummary = true, Order = 190)]
        [DataMember]
        public Hl7.Fhir.Model.FhirString Outbreak
        {
            get { if (_Outbreak == null) _Outbreak = new Hl7.Fhir.Model.FhirString(); return _Outbreak; }
            set { _Outbreak = value; OnPropertyChanged("Outbreak"); }
        }
        private Hl7.Fhir.Model.FhirString _Outbreak;

        /// <summary>
        /// The status of the case notification result
        /// </summary>
        [FhirElement("resultStatus", InSummary = true, Order = 110)]
        [DataMember]
        public Hl7.Fhir.Model.FhirString ResultStatus
        {
            get { if (_ResultStatus == null) _ResultStatus = new Hl7.Fhir.Model.FhirString(); return _ResultStatus; }
            set { _ResultStatus = value; OnPropertyChanged("ResultStatus"); }
        }
        private Hl7.Fhir.Model.FhirString _ResultStatus;

        /// <summary>
        /// An indication of where the disease or condition was likely acquired
        /// </summary>
        [FhirElement("importedIndicator", InSummary = true, Order = 180)]
        [DataMember]
        public Hl7.Fhir.Model.CodeableConcept ImportedIndicator
        {
            get { if (_ImportedIndicator == null) _ImportedIndicator = new Hl7.Fhir.Model.CodeableConcept(); return _ImportedIndicator; }
            set { _ImportedIndicator = value; OnPropertyChanged("ImportedIndicator"); }
        }
        private Hl7.Fhir.Model.CodeableConcept _ImportedIndicator;

        /// <summary>
        /// If the disease or condition was imported, this represents the originating location
        /// </summary>
        [FhirElement("importedAddress", InSummary = true, Order = 160)]
        [Cardinality(Min = 0, Max = -1)]
        [DataMember]
        public List<Hl7.Fhir.Model.Address> ImportedAddress
        {
            get { if (_ImportedAddress == null) _ImportedAddress = new List<Hl7.Fhir.Model.Address>(); return _ImportedAddress; }
            set { _ImportedAddress = value; OnPropertyChanged("ImportedAddress"); }
        }
        private List<Hl7.Fhir.Model.Address> _ImportedAddress;

        /// <summary>
        /// For cases meeting the binational criteria, select all the criteria which are met.
        /// </summary>
        [FhirElement("multinationalReportingCriteria", InSummary = true, Order = 160)]
        [Cardinality(Min = 0, Max = -1)]
        [DataMember]
        public List<Hl7.Fhir.Model.CodeableConcept> MultinationalReportingCriteria
        {
            get { if (_MultinationalReportingCriteria == null) _MultinationalReportingCriteria = new List<Hl7.Fhir.Model.CodeableConcept>(); return _MultinationalReportingCriteria; }
            set { _MultinationalReportingCriteria = value; OnPropertyChanged("MultinationalReportingCriteria"); }
        }
        private List<Hl7.Fhir.Model.CodeableConcept> _MultinationalReportingCriteria;

        public override void AddDefaultConstraints()
        {
            base.AddDefaultConstraints();
        }

        public override IDeepCopyable CopyTo(IDeepCopyable other)
        {
            var dest = other as Case;

            if (dest != null)
            {
                base.CopyTo(dest);
                if (Identifier != null) dest.Identifier = new List<Hl7.Fhir.Model.Identifier>(Identifier.DeepCopy());
                if (ActiveElement != null) dest.ActiveElement = (Hl7.Fhir.Model.FhirBoolean)ActiveElement.DeepCopy();
                if (Patient != null) dest.Patient = (Hl7.Fhir.Model.Patient)Patient.DeepCopy();
                if (ExposureAddress != null) dest.ExposureAddress = new List<Hl7.Fhir.Model.Address>(ExposureAddress.DeepCopy());
                if (TransmissionMode != null) dest.TransmissionMode = (Hl7.Fhir.Model.CodeableConcept)TransmissionMode.DeepCopy();
                if (ImmediateNotification != null) dest.ImmediateNotification = (Hl7.Fhir.Model.CodeableConcept)ImmediateNotification.DeepCopy();
                if (Outbreak != null) dest.Outbreak = (Hl7.Fhir.Model.FhirString)Outbreak.DeepCopy();
                if (ResultStatus != null) dest.ResultStatus = (Hl7.Fhir.Model.FhirString)ResultStatus.DeepCopy();
                if (ImportedIndicator != null) dest.ImportedIndicator = (Hl7.Fhir.Model.CodeableConcept)ImportedIndicator.DeepCopy();
                if (ImportedAddress != null) dest.ImportedAddress = new List<Hl7.Fhir.Model.Address>(ImportedAddress.DeepCopy());
                if (MultinationalReportingCriteria != null) dest.MultinationalReportingCriteria = new List<Hl7.Fhir.Model.CodeableConcept>(MultinationalReportingCriteria.DeepCopy());
                return dest;
            }
            else
                throw new ArgumentException("Can only copy to an object of the same type", "other");
        }

        public override IDeepCopyable DeepCopy()
        {
            return CopyTo(new Case());
        }

        [NotMapped]
        public override IEnumerable<Base> Children
        {
            get
            {
                foreach (var item in base.Children) yield return item;
                foreach (var elem in Identifier) { if (elem != null) yield return elem; }
                if (ActiveElement != null) yield return ActiveElement;
                if (Patient != null) yield return Patient;
                foreach (var elem in ExposureAddress) { if (elem != null) yield return elem; }
                if (TransmissionMode != null) yield return TransmissionMode;
                if (ImmediateNotification != null) yield return ImmediateNotification;
                if (Outbreak != null) yield return Outbreak;
                if (ResultStatus != null) yield return ResultStatus;
                if (ImportedIndicator != null) yield return ImportedIndicator;
                foreach (var elem in ImportedAddress) { if (elem != null) yield return elem; }
                foreach (var elem in MultinationalReportingCriteria) { if (elem != null) yield return elem; }
            }
        }

        [NotMapped]
        public override IEnumerable<ElementValue> NamedChildren
        {
            get
            {
                foreach (var item in base.NamedChildren) yield return item;
                foreach (var elem in Identifier) { if (elem != null) yield return new ElementValue("identifier", elem); }
                if (ActiveElement != null) yield return new ElementValue("active", ActiveElement);
                if (Patient != null) yield return new ElementValue("patient", Patient);
                foreach (var elem in ExposureAddress) { if (elem != null) yield return new ElementValue("exposureAddress", elem); }
                if (TransmissionMode != null) yield return new ElementValue("transmissionMode", TransmissionMode);
                if (ImmediateNotification != null) yield return new ElementValue("immediateNotification", ImmediateNotification);
                if (Outbreak != null) yield return new ElementValue("outbreak", Outbreak);
                if (ResultStatus != null) yield return new ElementValue("resultStatus", ResultStatus);
                if (ImportedIndicator != null) yield return new ElementValue("importedIndicator", ImportedIndicator);
                foreach (var elem in ImportedAddress) { if (elem != null) yield return new ElementValue("importedAddress", elem); }
                foreach (var elem in MultinationalReportingCriteria) { if (elem != null) yield return new ElementValue("multinationalReportingCriteria", elem); }
            }
        }
    }
}
