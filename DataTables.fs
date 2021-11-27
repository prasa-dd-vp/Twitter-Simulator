module DataTables
open System.Data

let dataSet = new DataSet()

//Tables
let userDT = new DataTable("User_Data_Table")
userDT.Columns.Add("")