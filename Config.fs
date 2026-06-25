namespace Functional.Esapi.PqmExtractor

module Config =

    /// Returns the hardcoded research patient ids.
    let patientIds =
        [
            "99DART-HN_1"
            "99DART-HN_2"
            "99DART-HN_3"
            "99DART-HN_4"
            "99DART-HN_5"
            "99DART-HN_6"
            "99DART-HN_7"
            "99DART-HN_8"
            "99DART-HN_9"
            "99DART-HN_10"
        ]

    /// Returns the hardcoded plan-quality metrics.
    let metrics =
        [
            { Id = "V95% PTV2"; Kind = MayoQuery("V95%[%]", "PTV2"); Unit = "%" }
            { Id = "V95% PTV3"; Kind = MayoQuery("V95%[%]", "PTV3"); Unit = "%" }
            { Id = "V107% PTV2-PTV1"; Kind = MayoQuery("V107%[%]", "PTV2-PTV1"); Unit = "%" }
            { Id = "V107% PTV3-PTV2"; Kind = MayoQuery("V107%[%]", "PTV3-PTV2"); Unit = "%" }
            { Id = "D1.8cm3 PTV1"; Kind = MayoQuery("D1.8cc[Gy]", "PTV1"); Unit = "Gy" }
        ]
