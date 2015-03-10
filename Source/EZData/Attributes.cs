using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace EZData
{

    /// <summary>
    /// Used to mark primary key columns in DBTable
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PrimaryKey : System.Attribute { }
}
