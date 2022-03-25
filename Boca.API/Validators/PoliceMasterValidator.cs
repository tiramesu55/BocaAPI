namespace BocaAPI.Validators
{
    using BocaAPI.Models.DTO;
    using FluentValidation;
    public class PoliceMasterValidator : AbstractValidator<VCSExport>
    {
        public PoliceMasterValidator()
        {
            RuleFor(r => r.PAYID).NotNull();
            RuleFor(r => r.WCPID).NotNull().MaximumLength(8);
            RuleFor(r => r.ReasonCode).MaximumLength(16);
            RuleFor(r => r.Reason).MaximumLength(128);
            RuleFor(r => r.ROSDT).NotNull();
            RuleFor(r => r.STRDT).NotNull();
            RuleFor(r => r.ENDDT).NotNull();
            RuleFor(r => r.SHFTAB).NotNull();
            RuleFor(r => r.REMOVED).NotNull();
            RuleFor(r => r.RECTYP).NotNull().MaximumLength(50);
            RuleFor(r => r.PAYDURAT).NotNull().ScalePrecision(3, 18);
            RuleFor(r => r.Comment).MaximumLength(1028);
        }
    }
}
