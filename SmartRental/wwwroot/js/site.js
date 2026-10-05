document.querySelectorAll('.dual-range').forEach(range => {
    const lowerLimit = Number(range.dataset.min);
    const upperLimit = Number(range.dataset.max);
    const minField = document.getElementById(range.dataset.minId);
    const maxField = document.getElementById(range.dataset.maxId);
    const minSlider = range.querySelector('.dual-range__min');
    const maxSlider = range.querySelector('.dual-range__max');
    const fill = range.querySelector('.dual-range__fill');
    const minLabel = range.parentElement.querySelector('[data-range-min-label]');
    const maxLabel = range.parentElement.querySelector('[data-range-max-label]');
    const presetButtons = range.parentElement.querySelectorAll('.quick-chips button');
    const isPrice = minField.id === 'minPrice';

    const formatValue = value => {
        if (!isPrice) return `${value}m²`;
        if (value < 1000000) return `${value / 1000}k`;
        return `${(value / 1000000).toFixed(1).replace('.0', '')}tr`;
    };

    const draw = () => {
        let minValue = Math.max(lowerLimit, Math.min(upperLimit, Number(minSlider.value)));
        let maxValue = Math.max(lowerLimit, Math.min(upperLimit, Number(maxSlider.value)));

        if (minValue > maxValue) {
            if (document.activeElement === minSlider) maxValue = minValue;
            else minValue = maxValue;
        }

        minSlider.value = minValue;
        maxSlider.value = maxValue;
        minSlider.setAttribute('aria-valuenow', minValue);
        maxSlider.setAttribute('aria-valuenow', maxValue);
        minSlider.setAttribute('aria-valuetext', formatValue(minValue));
        maxSlider.setAttribute('aria-valuetext', formatValue(maxValue));
        minField.value = minValue;
        maxField.value = maxValue;
        minLabel.textContent = formatValue(minValue);
        maxLabel.textContent = formatValue(maxValue);
        fill.style.left = `${(minValue - lowerLimit) / (upperLimit - lowerLimit) * 100}%`;
        fill.style.right = `${100 - (maxValue - lowerLimit) / (upperLimit - lowerLimit) * 100}%`;

        presetButtons.forEach(button => {
            const active = Number(button.dataset.min) === minValue && Number(button.dataset.max) === maxValue;
            button.classList.toggle('active', active);
            button.setAttribute('aria-pressed', active);
        });
    };

    [minSlider, maxSlider].forEach(slider => slider.addEventListener('input', draw));
    presetButtons.forEach(button => button.addEventListener('click', () => {
        minSlider.value = button.dataset.min;
        maxSlider.value = button.dataset.max;
        draw();
    }));

    draw();
});

const revealItems = document.querySelectorAll('[data-reveal]');
if (revealItems.length) {
    if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        const revealObserver = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-visible');
                revealObserver.unobserve(entry.target);
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -40px' });
        revealItems.forEach(item => revealObserver.observe(item));
    } else {
        revealItems.forEach(item => item.classList.add('is-visible'));
    }
}

// Animate real, integer statistics once when their section becomes visible.
document.querySelectorAll('[data-counter]').forEach(counter => {
    const target = Number.parseInt(counter.textContent.replace(/\D/g, ''), 10);
    if (!Number.isFinite(target) || target < 1) return;

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const run = () => {
        if (counter.dataset.counted === 'true') return;
        counter.dataset.counted = 'true';
        if (reducedMotion) {
            counter.textContent = target.toLocaleString('vi-VN');
            return;
        }

        const duration = 1000;
        const startedAt = performance.now();
        const tick = now => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            counter.textContent = Math.round(target * eased).toLocaleString('vi-VN');
            if (progress < 1) requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
    };

    if ('IntersectionObserver' in window && !reducedMotion) {
        const counterObserver = new IntersectionObserver(entries => {
            if (!entries[0].isIntersecting) return;
            run();
            counterObserver.disconnect();
        }, { threshold: 0.45 });
        counter.textContent = '0';
        counterObserver.observe(counter);
    } else {
        run();
    }
});

// Let the favorite feedback finish before the normal form submission.
document.querySelectorAll('.room-favorite').forEach(button => {
    button.addEventListener('click', event => {
        if (button.classList.contains('is-popping')) return;
        event.preventDefault();
        button.classList.add('is-popping');
        window.setTimeout(() => button.form?.requestSubmit(), 190);
    });
});

// Prevent accidental double submissions while preserving the clicked button's name/value.
document.querySelectorAll('form[data-single-submit]').forEach(form => {
    form.addEventListener('submit', event => {
        if (form.dataset.submitting === 'true') {
            event.preventDefault();
            return;
        }

        const submitter = event.submitter;
        if (submitter?.name) {
            const valueField = document.createElement('input');
            valueField.type = 'hidden';
            valueField.name = submitter.name;
            valueField.value = submitter.value;
            form.appendChild(valueField);
        }

        form.dataset.submitting = 'true';
        form.querySelectorAll('button[type="submit"], button:not([type])').forEach(button => {
            button.disabled = true;
        });

        if (submitter?.dataset.submitText) {
            const label = submitter.querySelector('span') ?? submitter;
            label.textContent = submitter.dataset.submitText;
        }
    });
});
