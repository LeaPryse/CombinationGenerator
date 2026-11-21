<div dir="rtl">

# מחולל התמורות – תשובות לשאלות המטלה

## 1. מהו מודל ניהול ה-STATE שבחרת ולמה?

**בצד הלקוח** בחרתי ב-**Signal Store**: service יחיד ב-Angular 20 ([permutation.store.ts](../client/src/app/state/permutation.store.ts)) ששומר את כל מצב המסך ב-signals פרטיים, וחושף לקומפוננטות גרסאות לקריאה בלבד ו-`computed` לערכים נגזרים. רק פעולות ה-store (`start`, `next`, `showAll`, `back`, `goToPage`) פונות לשרת ומעדכנות מצב. המצב `mode` הוא ערך יחיד (`input` / `single` / `all`), ולכן אי אפשר להגיע למצב סותר.

**בצד השרת** המצב הוא **סשן לכל משתמש**: אובייקט `PermutationSession` שמזוהה ב-GUID ושמור ב-`ISessionStore`. הלקוח שולח את המזהה בכל בקשה ב-header `X-Session-Id`. הסשן שומר רק את התמורה הנוכחית, האינדקס שלה ועוגן העימוד, כלומר זיכרון O(n) ולא O(n!).

**למה:**
- מקור אמת אחד למסך.
- המסך פשוט, ולכן NgRx (actions, reducers, effects) היה מיותר.
- Signals הם המנגנון המובנה של Angular 20, עם change detection מדויק ובלי ניהול subscriptions.
- קל לבדיקה: ה-API מוזרק דרך token וניתן להחלפה ב-mock.
- מספרים גדולים (`20!` חורג מ-`2^53`) נשמרים כמחרוזות ומטופלים ב-`BigInt`, כדי לא לאבד דיוק.

## 2. מה ההבדל בין NextPermutation ל-Index-Based, ומתי בחרת בכל אחד?

**NextPermutation** מחשב את התמורה העוקבת מהנוכחית, במקום: מוצאים מימין את `i` הראשון עם `a[i] < a[i+1]`, ואת `j` הראשון מימין עם `a[j] > a[i]`, מחליפים, והופכים את הסיומת אחרי `i`. זמן O(n) במקרה הגרוע ו-amortized O(1), בלי הקצאה. הוא זז רק קדימה, צעד אחד בכל פעם.

**Index-Based (`Unrank`)** מחשב ישירות את התמורה ה-k לפי מערכת מספרים פקטוריאלית: בכל מקום `k = index / (n-1-pos)!` בוחר איבר מהרשימה הנותרת, ו-`index %= (n-1-pos)!` ממשיך הלאה. זמן O(n²), בלי תלות באינדקס. לדוגמה, `n=3` ואינדקס 3 (0-based) נותן `[2,3,1]`.

**שימוש בפרויקט** ([PermutationSession.cs](../server/src/CombinationGenerator.Domain/Sessions/PermutationSession.cs)):
- כפתור **Next** משתמש ב-`NextPermutation`: צעד אחד זול.
- במצב **All**, הפריט הראשון בעמוד מחושב ב-`Unrank` (קפיצה ישירה לכל עמוד), ושאר הפריטים בעמוד ב-`NextPermutation`.
- שחזור סשן שמור (`Restore`) משתמש ב-`Unrank`.

כלומר Next לסריקה רציפה ו-Unrank לקפיצה, ושילוב ביניהם נותן עלות עמוד של O(n² + pageSize·n), בלי תלות ב-`n!`.

## 3. מה קורה כששלושה משתמשים נכנסים בו-זמנית? למה?

כל משתמש מקבל סשן נפרד עם GUID משלו, ולכן אין ביניהם מצב משותף. בנוסף, ל-`PermutationSession` יש מצב שמשתנה במקום ואינו thread-safe, ולכן ב-[InMemorySessionStore.cs](../server/src/CombinationGenerator.Infrastructure/Sessions/InMemorySessionStore.cs) לכל סשן יש `SemaphoreSlim` משלו, וכל פעולה רצה תחתיו.

- משתמשים שונים לא חוסמים זה את זה (נעילות שונות).
- בקשות מקבילות של אותו משתמש (למשל שתי לחיצות מהירות) מסודרות בתור, ולכן אין תמורה שנדלגת, חוזרת או נהרסת.
- החיפוש ב-`ConcurrentDictionary` ללא נעילה גלובלית. נעילה גלובלית קיימת רק בהוספת סשן.
- הגנות: תפוגה אחרי 30 דקות ללא פעילות, ותקרה של 10,000 סשנים (אחריה `503`, ובלי פינוי של סשן חי).

## 4. אילו 2 שינויים תעשה בשרת כדי להוסיף שמירה ל-Redis?

1. **מימוש חדש של `ISessionStore`** (`RedisSessionStore`) בשכבת Infrastructure. הוא שומר את `id`, `n`, `currentIndex` ו-`pagingAnchor` עם TTL של 30 דקות, ובטעינה משחזר את הסשן עם `PermutationSession.Restore`, שמחשב את התמורה מחדש עם `Unrank`. כדי לשמור על סידור הבקשות של אותו סשן בין כמה instances של שרת, הנעילה צריכה להיות **מבוזרת** (למשל `SET NX PX` או RedLock) ולא `SemaphoreSlim`.
2. **החלפת הרישום ב-DI והוספת קונפיגורציה** ב-[DependencyInjection.cs](../server/src/CombinationGenerator.Infrastructure/DependencyInjection.cs): רישום `RedisSessionStore` במקום `InMemorySessionStore`, רישום `IConnectionMultiplexer`, והוספת connection string ל-`appsettings`.

שכבות Domain, Application ו-Api לא משתנות, כי `PermutationService` תלוי רק בממשק [ISessionStore.cs](../server/src/CombinationGenerator.Application/Abstractions/ISessionStore.cs), שכבר אסינכרוני ותומך ב-`CancellationToken`.

## 5. מה החיסרון העיקרי של NextPermutation עבור n = 20?

הוא יכול לזוז רק צעד אחד בכל פעם, ולכן כדי להגיע לתמורה מספר k צריך לעבור על כל k התמורות שלפניה. עבור `n = 20` יש `20! = 2,432,902,008,176,640,000` תמורות, ואפילו בקצב של מיליארד תמורות בשנייה מעבר על כולן ייקח כ-77 שנים. לכן `NextPermutation` לבדו אינו מאפשר קפיצה לעמוד שרירותי (למשל העמוד האחרון), ואי אפשר גם לשמור את הרשימה בזיכרון.

הפתרון: `Unrank` מחשב את הפריט הראשון של כל עמוד ב-O(n²), ו-`NextPermutation` ממלא את שאר העמוד.

</div>
