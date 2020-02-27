# Public Health Data Processing Pipeline for Case-Based Surveillance

This project contains a proof-of-concept (PoC) data processing pipeline for national-level case-based surveillance. It shows how a national health agency could ingest, process, and provision case notifications received from sub-national entities, such as states, provinces, or counties. The PoC remains an early work-in-progress and is not intended for production use cases.

The PoC implements the pipeline on Azure Functions. Other Azure PaaS offerings are under consideration.

In the U.S., case notifications are currently received in the HL7 v2.5.1 and NETSS file formats, with the former format being preferred. The PoC shows how these HL7 v2.5.1 messages can be converted into an HL7 FHIR R4 message using an Azure Function that runs as part of the data processing pipeline. Conversion from NETSS to HL7 FHIR R4 is under consideration. 

The pipeline's FHIR implementation should adhere to the [electronic initial case report implementation guide](http://build.fhir.org/ig/HL7/case-reporting/Electronic_Initial_Case_Report_(eICR)_Transaction_and_Profiles.html), also known as just __eICR__.

Messages are received by an Azure Function that is triggered every time an HTTP call is made to it. Subsequent triggers run a series of Azure Functions that carry out message storage, validation, conversion, and transformation.

This PoC specifically uses [Azure Durable Functions](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=csharp).

## FHIR conversion from HL7 v2.5.1 to HL7 FHIR R4

The target FHIR implementation guide is the [eICR](http://build.fhir.org/ig/HL7/case-reporting/Electronic_Initial_Case_Report_(eICR)_Transaction_and_Profiles.html).

Incoming HL7 v2.5.1 messages are in the `ORU_R01` format. These HL7 v2.5.1 messages will adhere to one of many CDC-published 'message mapping guides' (MMGs), all of which can be found publicly at https://wwwn.cdc.gov/nndss/case-notification/message-mapping-guides.html. CDC publishes one MMG per set of public health conditions. For example, there are separate MMGs for sexually-transmitted diseases, Hepatitis, Malaria, Mumps, and Pertussis. 

Every MMG inherits a core set of data elements that are generic across all conditions. These core data elements are found in the ["Generic" MMG](https://wwwn.cdc.gov/nndss/document/Generic_V2.0_MMG_F_R5_20171206.xlsx).

Each of CDC's message mapping guides has an accompanying ZIP file with about eight example HL7 v2.5.1 messages that were built to conform to the guide.

The technical approach to converting from HL7 2.5.1 to FHIR is to first logically map data elements from the MMGs into FHIR resources. Once this logical mapping exists, code can be written that will locate those data elements in the HL7 2.5.1 message and extract the appropriate values. Finally, more code can be written that will construct the series of FHIR resources needed to adhere to eICR.

See `LegacyToEcrFhirConverter.cs` in the `/thunderpeak-receiver/Converters` folder for the actual code.

## Running the pipeline locally for development and debugging purposes

Recommended tools:

* [Postman](https://www.postman.com/)
* [Azure Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator)
* [Visual Studio 2019 Community](https://visualstudio.microsoft.com/vs/)

> Note that Azure Storage Emulator is Windows-only at this time.

Steps to run locally:
1. Clone this repository
1. Open the `.sln` file located in the repository root. Windows 10 should open it in Visual Studio 2019.
1. Press the green __Debug__ button at the top of the Visual Studio 2019 window. The function app starts in a terminal session after several seconds. Take note of the API endpoint that the terminal displays, which will be typically be something like `http://localhost:7071/api/ProcessMessageStartup`
1. Open the **Azure Storage Emulator**
1. Navigate to **Local & Attached** > **Storage Accounts** > **(Emulator - Default Ports)** > **Blob Containers** and keep the window open. 
1. Open **Postman**
1. Create a new HTTP POST request in Postman with a body type of **raw** and the following body content:

```
MSH|^~\&|SendAppName^2.16.840.1.114222.nnnn^ISO|Sending-Facility^2.16.840.1.114222.nnnn^ISO|PHINCDS^2.16.840.1.114222.4.3.2.10^ISO|PHIN^2.16.840.1.114222^ISO|20141225120030.1234-0500||ORU^R01^ORU_R01|STD_V1_CN_TM_TC03|T|2.5.1|||||||||NOTF_ORU_v3.0^PHINProfileID^2.16.840.1.114222.4.10.3^ISO~Generic_MMG_V2.0^PHINMsgMapID^2.16.840.1.114222.4.10.4^ISO~STD_MMG_V1.0^PHINMsgMapID^2.16.840.1.114222.4.10.4^ISO
PID|1||STD_TC03^^^SendAppName&2.16.840.1.114222.GENv2&ISO||~^^^^^^S||19980407|M||2054-5^Black or African American^CDCREC|^^^35^87004^^^^35043|||||||||||2186-5^Not Hispanic or Latino^CDCREC
OBR|1||STD_TC03^SendAppName^2.16.840.1.114222.nnnn^ISO|68991-9^Epidemiologic Information^LN|||20140116170100|||||||||||||||20140116170100|||F||||||10320^Syphilis, unknown duration or late^NND
OBX|1|CWE|78746-5^Country of Birth^LN||USA^United States^ISO3166_1||||||F
OBX|2|CWE|77983-5^Country of Usual Residence^LN||USA^United States^ISO3166_1||||||F
OBX|3|TS|11368-8^Date of Illness Onset^LN||20140112||||||F
OBX|4|TS|77976-9^Illness End Date^LN||20140120||||||F
```

8. Submit the request in Postman. You should receive an HTTP 202 response code.
1. Navigate back to the Azure Storage Emulator window. Press the **Refresh all** link. A series of containers should appear in the **Blob Containers** list:

* **messages-raw** stores the raw message exactly as the pipeline received it
* **messages-fhir** stores the message after it's been converted to FHIR
* **messages-flattened** stores the message after it's been transformed from FHIR into a flatter, easier-to-analyze format

10. If you only submitted one HTTP POST in Postman, there should only be one file in each of the three containers. Open each container and look for the file in each one. Double-clicking on the file will open it in a text editor.
