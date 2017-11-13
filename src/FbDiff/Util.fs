namespace FbDiff

open System.Text

module Util =
    let concatLines strings = strings |> Seq.fold (fun (sb:StringBuilder) x -> sb.AppendLine x) (StringBuilder()) |> fun x->x.ToString()
    let intersperse sep ls =
            Seq.foldBack (fun x -> function 
                | [] -> [x]
                | xs -> x::sep::xs) ls []
    let mkstring sep ls = ls |> intersperse sep |> Seq.fold (+) ""
    //let (?=) s1 s2 = String.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase)
