using System.Runtime.CompilerServices;

// Тести перевіряють і внутрішні мутатори моделі (BossModel.PaintSegment, MoveResult.Add тощо):
// робити їх public лише заради тестів означало б відкрити геймплею шляхи змінювати стан
// в обхід правил. InternalsVisibleTo лишає API вузьким, але тестованим.
[assembly: InternalsVisibleTo("InkFlow.Core.Tests")]
