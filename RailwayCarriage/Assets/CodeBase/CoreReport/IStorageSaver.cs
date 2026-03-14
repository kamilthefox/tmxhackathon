using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IStorageSaver
{
    void Save(StorageGarbage.StorageData storage);
    void Load(StorageGarbage.StorageData storage);

}

