using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Enums
{
    /// <summary>
    /// Specifies the types of dynamic fields that can be used in a form or data entry context.
    /// </summary>
    public enum DynamicFieldType
    {
        Text,
        TextInput,
        Number,
        Dropdown,
        Checkbox,
        Agreement
    }
}
