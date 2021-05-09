using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Core;

namespace NetPrintsEditor.Dialogs
{
    public class SelectTypeDialog : SearchableComboboxDialog
    {
        public SelectTypeDialog() : 
            base("Select Type", App.NonStaticTypes, TypeSpecifier.FromType<object>())
        { }

        public static bool Prompt(out TypeSpecifier type)
        {
            var selectTypeDialog = new SelectTypeDialog();
            if (selectTypeDialog.ShowDialog() == true)
            {
                type = (TypeSpecifier) selectTypeDialog.SelectedItem;
                if (type is not null && type.Equals(null) == false)
                {
                    return true;
                }
            }

            type = default;
            return false;
        }
    }
}
