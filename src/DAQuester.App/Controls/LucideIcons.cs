using Avalonia.Media;

namespace DAQuester.App.Controls;
// Lucide SVG geometry, converted without changing its shapes.
// Source: https://github.com/lucide-icons/lucide/tree/70562c1ee1c4fdcf736fe97bc893fb8511927934/icons
// ISC and Feather MIT notices: Licenses/Lucide/LICENSE.txt.
internal static class LucideIcons
{
    private static readonly IReadOnlyDictionary<string, Geometry> Icons = new Dictionary<string, Geometry>
    {
        ["BookOpen"] = Combine("M12 5v16", "M20.001 19A2 2 0 0022 17V5a2 2 0 00-1.999-2L16 3.002A5 5 0 0012 5a5 5 0 00-4-2H4a2 2 0 00-2 2v12a2 2 0 001.999 2H8a5 5 0 014 2 5 5 0 014-2z"),
        ["Users"] = Combine("M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2", "M16 3.128a4 4 0 0 1 0 7.744", "M22 21v-2a4 4 0 0 0-3-3.87", "M5,7 A4,4 0 1 0 13,7 A4,4 0 1 0 5,7 Z"),
        ["RotateCcw"] = Combine("M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8", "M3 3v5h5"),
        ["Settings"] = Combine("M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915", "M9,12 A3,3 0 1 0 15,12 A3,3 0 1 0 9,12 Z"),
        ["ShieldCheck"] = Combine("M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z", "m9 12 2 2 4-4"),
        ["Square"] = Combine("M5,3 H19 Q21,3 21,5 V19 Q21,21 19,21 H5 Q3,21 3,19 V5 Q3,3 5,3 Z"),
        ["Check"] = Combine("M20 6 9 17l-5-5"),
        ["X"] = Combine("M18 6 6 18", "m6 6 12 12"),
        ["GripVertical"] = Combine("M8,12 A1,1 0 1 0 10,12 A1,1 0 1 0 8,12 Z", "M8,5 A1,1 0 1 0 10,5 A1,1 0 1 0 8,5 Z", "M8,19 A1,1 0 1 0 10,19 A1,1 0 1 0 8,19 Z", "M14,12 A1,1 0 1 0 16,12 A1,1 0 1 0 14,12 Z", "M14,5 A1,1 0 1 0 16,5 A1,1 0 1 0 14,5 Z", "M14,19 A1,1 0 1 0 16,19 A1,1 0 1 0 14,19 Z"),
        ["Sparkles"] = Combine("M11.017 2.814a1 1 0 0 1 1.966 0l1.051 5.558a2 2 0 0 0 1.594 1.594l5.558 1.051a1 1 0 0 1 0 1.966l-5.558 1.051a2 2 0 0 0-1.594 1.594l-1.051 5.558a1 1 0 0 1-1.966 0l-1.051-5.558a2 2 0 0 0-1.594-1.594l-5.558-1.051a1 1 0 0 1 0-1.966l5.558-1.051a2 2 0 0 0 1.594-1.594z", "M20 2v4", "M22 4h-4", "M2,20 A2,2 0 1 0 6,20 A2,2 0 1 0 2,20 Z"),
        ["GitBranch"] = Combine("M15 6a9 9 0 0 0-9 9V3", "M15,6 A3,3 0 1 0 21,6 A3,3 0 1 0 15,6 Z", "M3,18 A3,3 0 1 0 9,18 A3,3 0 1 0 3,18 Z"),
        ["Scissors"] = Combine("M3,6 A3,3 0 1 0 9,6 A3,3 0 1 0 3,6 Z", "M8.12 8.12 12 12", "M20 4 8.12 15.88", "M3,18 A3,3 0 1 0 9,18 A3,3 0 1 0 3,18 Z", "M14.8 14.8 20 20"),
        ["Gem"] = Combine("M10.5 3 8 9l4 13 4-13-2.5-6", "M17 3a2 2 0 0 1 1.6.8l3 4a2 2 0 0 1 .013 2.382l-7.99 10.986a2 2 0 0 1-3.247 0l-7.99-10.986A2 2 0 0 1 2.4 7.8l2.998-3.997A2 2 0 0 1 7 3z", "M2 9h20"),
        ["Sprout"] = Combine("M14 9.536V7a4 4 0 0 1 4-4h1.5a.5.5 0 0 1 .5.5V5a4 4 0 0 1-4 4 4 4 0 0 0-4 4c0 2 1 3 1 5a5 5 0 0 1-1 3", "M4 9a5 5 0 0 1 8 4 5 5 0 0 1-8-4", "M5 21h14"),
        ["ScrollText"] = Combine("M15 12h-5", "M15 8h-5", "M19 17V5a2 2 0 0 0-2-2H4", "M8 21h12a2 2 0 0 0 2-2v-1a1 1 0 0 0-1-1H11a1 1 0 0 0-1 1v1a2 2 0 1 1-4 0V5a2 2 0 1 0-4 0v2a1 1 0 0 0 1 1h3"),
        ["Folder"] = Combine("M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"),
        ["File"] = Combine("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z", "M14 2v5a1 1 0 0 0 1 1h5"),
        ["ArrowUp"] = Combine("m5 12 7-7 7 7", "M12 19V5"),
        ["ArrowDown"] = Combine("M12 5v14", "m19 12-7 7-7-7"),
        ["Play"] = Combine("M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z"),
        ["Pause"] = Combine("M15 3 H18 Q19 3 19 4 V20 Q19 21 18 21 H15 Q14 21 14 20 V4 Q14 3 15 3 Z", "M6 3 H9 Q10 3 10 4 V20 Q10 21 9 21 H6 Q5 21 5 20 V4 Q5 3 6 3 Z"),
        ["ListOrdered"] = Combine("M11 5h10", "M11 12h10", "M11 19h10", "M4 4h1v5", "M4 9h2", "M6.5 20H3.4c0-1 2.6-1.925 2.6-3.5a1.5 1.5 0 0 0-2.6-1.02"),
        ["Plus"] = Combine("M5 12h14", "M12 5v14"),
        ["ChevronUp"] = Combine("m18 15-6-6-6 6"),
        ["ChevronDown"] = Combine("m6 9 6 6 6-6"),
        ["CircleHelp"] = Combine("M2,12 A10,10 0 1 0 22,12 A10,10 0 1 0 2,12 Z", "M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3", "M12 17h.01"),
    };
    // SVG elements each start at their own origin. Separate geometries preserve
    // relative move commands instead of continuing from the previous element.
    private static Geometry Combine(params string[] paths)
    {
        var group = new GeometryGroup();
        foreach (string path in paths)
            group.Children.Add(Geometry.Parse(path));
        return group;
    }

    public static Geometry Get(string name) => Icons[name];
}
