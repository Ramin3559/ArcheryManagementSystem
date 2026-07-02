using EShooting.Domain.Enums;

namespace EShooting.Web.Helpers;

public static class CustomerDisplayHelper
{
    public static string FormatCategory(CustomerCategory category) => category switch
    {
        CustomerCategory.Amateur => "Həvəskar",
        CustomerCategory.Professional => "Professional",
        CustomerCategory.Coach => "Məşqçi",
        _ => category.ToString()
    };
}
