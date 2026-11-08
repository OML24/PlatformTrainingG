namespace ZuLms.Application.Common.Abstractions;

/// <summary>Input for drawing the certificate text layer on top of the uploaded template PDF.</summary>
public sealed record CertificateTextLayerInput(
    decimal PageWidthPt, decimal PageHeightPt, string LayoutJson,
    string HolderNameAr, string? HolderNameEn,
    string CourseTitleAr, string? CourseTitleEn,
    string CoursePeriodText, string CourseHoursText,
    string InstructorNameAr, string? InstructorNameEn,
    string IssueDateText, string ReferenceNo, string VerifyUrl);

/// <summary>Renders the final certificate PDF by overlaying the text layer on the template.</summary>
public interface ICertificatePdfRenderer
{
    /// <summary>Returns the merged PDF bytes (template + transparent text layer with QR).</summary>
    byte[] Render(byte[] templatePdf, CertificateTextLayerInput input, byte[] qrPng);
}
