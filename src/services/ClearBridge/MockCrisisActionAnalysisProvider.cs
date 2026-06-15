using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class MockCrisisActionAnalysisProvider : ICrisisActionAnalysisProvider
    {
        public const string SampleNotice =
            "Due to extreme weather conditions, all outdoor extracurricular activities scheduled after 12:30 PM today are suspended. Students should check with their activity coordinators for indoor alternatives. Bus schedules have not been confirmed to change. Parents should monitor the school portal for further updates.";

        public string Name => "Mock";

        public Task<CrisisActionAnalysisResult> AnalyzeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = outputLanguage switch
            {
                ClearBridgeOutputLanguages.SimplifiedChinese => BuildChineseResult(),
                ClearBridgeOutputLanguages.Arabic => BuildArabicResult(),
                ClearBridgeOutputLanguages.Spanish => BuildSpanishResult(),
                ClearBridgeOutputLanguages.French => BuildFrenchResult(),
                _ => BuildEnglishResult()
            };
            return Task.FromResult(result);
        }

        private static CrisisActionAnalysisResult BuildEnglishResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Outdoor activities suspended after 12:30 PM",
                Summary = "Because of extreme weather, outdoor extracurricular activities after 12:30 PM today are suspended. Students need to ask activity coordinators about indoor alternatives, and parents should watch the school portal for updates.",
                Priority = "high",
                ImportantPoints =
                [
                    "Outdoor extracurricular activities after 12:30 PM today are suspended.",
                    "Students may have indoor alternatives, but they must confirm them with activity coordinators.",
                    "Bus schedule changes are not confirmed yet.",
                    "Parents should monitor the school portal for updates."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "Ask the activity coordinator whether there is an indoor alternative.",
                        Deadline = "Today, before the scheduled activity after 12:30 PM",
                        Location = "School or activity coordinator contact channel",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "Check the school portal for further updates.",
                        Deadline = "Today and until the school posts a new update",
                        Location = "School portal",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "The notice does not confirm whether bus schedules will change.",
                    "The notice does not list specific indoor alternatives."
                ],
                Warnings =
                [
                    "Do not assume the bus schedule has changed unless the school confirms it.",
                    "Outdoor activities should be treated as suspended after 12:30 PM today."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "Outdoor activities after 12:30 PM are suspended.",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Students should ask about indoor alternatives.",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Bus changes are unclear.",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Parents should watch for updates.",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildChineseResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "下午 12:30 后户外活动暂停",
                Summary = "由于极端天气，今天下午 12:30 后安排的户外课外活动暂停。学生应联系活动负责人确认是否有室内替代安排，家长应继续查看学校 portal 的更新。",
                Priority = "high",
                ImportantPoints =
                [
                    "今天下午 12:30 后的户外课外活动暂停。",
                    "可能有室内替代安排，但需要向活动负责人确认。",
                    "校车时间是否改变尚未确认。",
                    "家长需要关注学校 portal 后续通知。"
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "联系活动负责人，确认是否有室内替代安排。",
                        Deadline = "今天，原定下午 12:30 后活动开始前",
                        Location = "学校或活动负责人联系渠道",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "查看学校 portal 是否发布进一步更新。",
                        Deadline = "今天以及学校发布新通知前",
                        Location = "学校 portal",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "通知没有确认校车安排是否改变。",
                    "通知没有列出具体室内替代活动。"
                ],
                Warnings =
                [
                    "不要在学校确认前假设校车时间已经改变。",
                    "今天下午 12:30 后的户外活动应视为暂停。"
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "下午 12:30 后户外活动暂停。",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "学生应确认室内替代安排。",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "校车安排仍不明确。",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "家长应查看学校 portal。",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildArabicResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "تعليق الأنشطة الخارجية بعد 12:30 ظهرا",
                Summary = "بسبب الطقس القاسي، تم تعليق الأنشطة اللامنهجية الخارجية المجدولة بعد 12:30 ظهرا اليوم. يجب على الطلاب سؤال منسقي الأنشطة عن البدائل الداخلية، وعلى أولياء الأمور متابعة بوابة المدرسة للتحديثات.",
                Priority = "high",
                ImportantPoints =
                [
                    "تم تعليق الأنشطة اللامنهجية الخارجية بعد 12:30 ظهرا اليوم.",
                    "قد توجد بدائل داخلية، لكن يجب تأكيدها مع منسقي الأنشطة.",
                    "لم يتم تأكيد أي تغيير في جداول الحافلات.",
                    "يجب على أولياء الأمور متابعة بوابة المدرسة للتحديثات."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "اسأل منسق النشاط عما إذا كان هناك بديل داخلي.",
                        Deadline = "اليوم، قبل النشاط المجدول بعد 12:30 ظهرا",
                        Location = "المدرسة أو قناة التواصل مع منسق النشاط",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "تحقق من بوابة المدرسة لأي تحديثات لاحقة.",
                        Deadline = "اليوم وحتى تنشر المدرسة تحديثا جديدا",
                        Location = "بوابة المدرسة",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "لا يؤكد الإشعار ما إذا كانت جداول الحافلات ستتغير.",
                    "لا يذكر الإشعار البدائل الداخلية المحددة."
                ],
                Warnings =
                [
                    "لا تفترض أن جدول الحافلات تغير إلا إذا أكدت المدرسة ذلك.",
                    "يجب اعتبار الأنشطة الخارجية معلقة بعد 12:30 ظهرا اليوم."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "تم تعليق الأنشطة الخارجية بعد 12:30 ظهرا.",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "يجب على الطلاب السؤال عن البدائل الداخلية.",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "تغييرات الحافلات غير مؤكدة.",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "يجب على أولياء الأمور متابعة التحديثات.",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildSpanishResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Actividades al aire libre suspendidas después de las 12:30 p. m.",
                Summary = "Debido al clima extremo, se suspenden las actividades extracurriculares al aire libre programadas después de las 12:30 p. m. de hoy. Los estudiantes deben preguntar a sus coordinadores sobre alternativas en interiores, y los padres deben revisar el portal escolar.",
                Priority = "high",
                ImportantPoints =
                [
                    "Las actividades extracurriculares al aire libre después de las 12:30 p. m. de hoy están suspendidas.",
                    "Puede haber alternativas en interiores, pero los estudiantes deben confirmarlas con los coordinadores.",
                    "No se ha confirmado si cambiarán los horarios de autobuses.",
                    "Los padres deben revisar el portal escolar para más actualizaciones."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "Preguntar al coordinador de la actividad si existe una alternativa en interiores.",
                        Deadline = "Hoy, antes de la actividad programada después de las 12:30 p. m.",
                        Location = "Escuela o canal de contacto del coordinador",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "Revisar el portal escolar para futuras actualizaciones.",
                        Deadline = "Hoy y hasta que la escuela publique una nueva actualización",
                        Location = "Portal escolar",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "El aviso no confirma si cambiarán los horarios de autobuses.",
                    "El aviso no enumera alternativas específicas en interiores."
                ],
                Warnings =
                [
                    "No asuma que el horario de autobuses cambió a menos que la escuela lo confirme.",
                    "Las actividades al aire libre deben considerarse suspendidas después de las 12:30 p. m. de hoy."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "Las actividades al aire libre después de las 12:30 p. m. están suspendidas.",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Los estudiantes deben preguntar por alternativas en interiores.",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Los cambios de autobús no están claros.",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Los padres deben revisar actualizaciones.",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildFrenchResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Activités extérieures suspendues après 12 h 30",
                Summary = "En raison de conditions météo extrêmes, les activités périscolaires extérieures prévues après 12 h 30 aujourd'hui sont suspendues. Les élèves doivent demander aux coordinateurs s'il existe des alternatives en intérieur, et les parents doivent consulter le portail de l'école.",
                Priority = "high",
                ImportantPoints =
                [
                    "Les activités périscolaires extérieures après 12 h 30 aujourd'hui sont suspendues.",
                    "Des alternatives en intérieur peuvent exister, mais elles doivent être confirmées avec les coordinateurs.",
                    "Aucun changement des horaires de bus n'est confirmé.",
                    "Les parents doivent surveiller le portail de l'école pour les mises à jour."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "Demander au coordinateur de l'activité s'il existe une alternative en intérieur.",
                        Deadline = "Aujourd'hui, avant l'activité prévue après 12 h 30",
                        Location = "École ou canal de contact du coordinateur",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "Consulter le portail de l'école pour les prochaines mises à jour.",
                        Deadline = "Aujourd'hui et jusqu'à une nouvelle mise à jour de l'école",
                        Location = "Portail de l'école",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "L'avis ne confirme pas si les horaires de bus vont changer.",
                    "L'avis ne liste pas d'alternatives précises en intérieur."
                ],
                Warnings =
                [
                    "Ne supposez pas que l'horaire des bus a changé sans confirmation de l'école.",
                    "Les activités extérieures doivent être considérées comme suspendues après 12 h 30 aujourd'hui."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "Les activités extérieures après 12 h 30 sont suspendues.",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Les élèves doivent demander les alternatives en intérieur.",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Les changements de bus ne sont pas confirmés.",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Les parents doivent surveiller les mises à jour.",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }
    }
}
