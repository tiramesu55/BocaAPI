namespace BocaAPI.Validators
{
    using BocaAPI.Models.DTO;
    using FluentValidation;
    public class PoliceMasterValidator : AbstractValidator<VCSExport>
    {
        public static readonly DateTime SmallDateTimeMin = new(1900, 1, 1);
        public static readonly DateTime SmallDateTimeMax = new(2079, 6, 6, 23, 59, 0);

        public PoliceMasterValidator( List<string> acceptableCodes)
        {
            RuleFor(r => r.PAYID).NotNull();
            RuleFor(r => r.WCPID).NotEmpty().MaximumLength(8).Must(r => acceptableCodes.Contains(r)); //make sure that ReasonCode can only have certain values

            RuleFor(r => r.ReasonCode).MaximumLength(16);
            RuleFor(r => r.Reason).MaximumLength(128);
            //police_master date columns are smalldatetime; a date outside its range fails the SQL insert
            RuleFor(r => r.ROSDT).Cascade(CascadeMode.Stop).NotEmpty().InclusiveBetween(SmallDateTimeMin, SmallDateTimeMax);
            RuleFor(r => r.STRDT).Cascade(CascadeMode.Stop).NotEmpty().InclusiveBetween(SmallDateTimeMin, SmallDateTimeMax);  //STRDT is the exported date, so it must be a real date
            RuleFor(r => r.ENDDT).Cascade(CascadeMode.Stop).NotEmpty().InclusiveBetween(SmallDateTimeMin, SmallDateTimeMax);
            RuleFor(r => r.SHFTAB).NotNull();
            RuleFor(r => r.REMOVED).NotNull();
            RuleFor(r => r.PAYDURAT).NotNull().ScalePrecision(3, 18);
            RuleFor(r => r.Comment).MaximumLength(1028);
        }
    }
}
