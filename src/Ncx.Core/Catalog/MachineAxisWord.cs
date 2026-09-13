namespace Ncx.Core.Catalog;

/// <summary>
/// A machine axis word of D93: a key the catalog does not list that has the form of an axis name, [XYZABCUVW] and up
/// to two digits, or I followed by that: Z2, IZ2, W, C2 (language 3, KEY; 4.3). The virtual machine resolves the axis
/// name against the [[axis]] list of the machine configuration; the canonical order puts machine axes after
/// X Y Z A B C, by letter and then by number, the absolute words before the incremental ones (language 5 rule 6, D90).
/// </summary>
/// <param name="Key">The key as written: "IZ2".</param>
/// <param name="AxisName">The axis the word moves, without the I of the incremental form: "Z2".</param>
/// <param name="Letter">The letter of the axis name: 'Z'.</param>
/// <param name="Number">The number after the letter: 2; null for an axis name of one letter, such as W.</param>
/// <param name="IsIncremental">True for the incremental form, I followed by the axis name.</param>
public sealed record MachineAxisWord(string Key, string AxisName, char Letter, int? Number, bool IsIncremental);
