using System.Collections.Generic;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Пул крапель ігрового поля. Єдине місце, якому дозволено інстанціювати
    /// краплю — і робить це воно лише при наповненні, до першого ходу.
    ///
    /// Під час партії Instantiate/Destroy заборонені: ланцюг вибухів звільняє
    /// й займає десятки клітинок за секунду, і GC-спайк на мобільному читається
    /// як фриз рівно в найдорожчий момент.
    ///
    /// Порожня клітинка — це відсутність краплі, а не прозорий плейсхолдер:
    /// плейсхолдери лишалися б у канвасі й перебудовувались разом з усіма.
    /// </summary>
    public sealed class DropPool : MonoBehaviour
    {
        [SerializeField] private DropView prefab;

        [Tooltip("Куди складати краплі. Зазвичай — полотно поля.")]
        [SerializeField] private RectTransform contentRoot;

        [Tooltip("Наперед: 8×8 = 64 з запасом на краплю, що летить при злитті.")]
        [SerializeField, Min(1)] private int prewarm = 68;

        private readonly Stack<DropView> _idle = new Stack<DropView>(72);
        private readonly List<DropView> _all = new List<DropView>(72);

        /// <summary>Скільки крапель зараз на полі — тест перевіряє, що пул не тече.</summary>
        public int ActiveCount => _all.Count - _idle.Count;

        // Наповнюємо у Start, а не в Awake: у Awake Unity ще не розсилає
        // SendMessage-колбеки, і кожен створений об'єкт дає пачку попереджень.
        private void Start() => Prewarm();

        private void Prewarm()
        {
            if (prefab == null || contentRoot == null)
            {
                Debug.LogError("[InkFlow] DropPool: не призначено префаб або корінь. " +
                               "Перезбери сцену: Ink Flow → Setup → Build Level Screen.", this);
                enabled = false;
                return;
            }

            while (_all.Count < prewarm)
                _idle.Push(Create());
        }

        private DropView Create()
        {
            var view = Instantiate(prefab, contentRoot);
            view.gameObject.SetActive(false);
            _all.Add(view);
            return view;
        }

        public DropView Get()
        {
            if (prefab == null || contentRoot == null)
                return null!;

            // Якщо пул вичерпано — розширюємось, але це вже поза бюджетом:
            // сітка більша за prewarm означає, що prewarm треба піднімати.
            var view = _idle.Count > 0 ? _idle.Pop() : Create();
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(DropView view)
        {
            if (view == null)
                return;

            view.SetNearMiss(false);
            view.transform.localScale = Vector3.one;
            view.transform.localRotation = Quaternion.identity;
            view.gameObject.SetActive(false);
            _idle.Push(view);
        }
    }
}
