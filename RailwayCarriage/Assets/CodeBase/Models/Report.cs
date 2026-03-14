using System;
using System.Collections.Generic;

[Serializable]
public class Report
{
    public int user_id;
    public int id_cat;
    public int id_line;
    public string geom;    
    public string report_text;
    public string number_carriage;
    public string number_train;
}

[Serializable]
public class CategoriesResponse
{
    public bool success;
    public int count;
    public List<CategoryData> categories;
}

[Serializable]
public class CategoryData
{
    public int id_cat;
    public string name;
}
