using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


internal interface IUserSaver
{
    void Save(User user);
    User Load();
    bool HasSavedData();
}