// WebAuthn API インタラクション用JavaScript
// パスキー登録・認証を処理

class PasskeySubmit extends HTMLElement {
    connectedCallback() {
        const form = this.closest('form');
        if (!form) {
            console.error('PasskeySubmit: フォームが見つかりません');
            return;
        }

        const submitButton = form.querySelector('button[name="__passkeySubmit"]');
        if (!submitButton) {
            console.error('PasskeySubmit: 送信ボタンが見つかりません');
            return;
        }

        submitButton.addEventListener('click', async (e) => {
            e.preventDefault();
            await this.handlePasskeyOperation(form);
        });
    }

    async handlePasskeyOperation(form) {
        const operation = this.getAttribute('operation');
        const name = this.getAttribute('name');
        const emailName = this.getAttribute('email-name');
        const requestTokenName = this.getAttribute('request-token-name');
        const requestTokenValue = this.getAttribute('request-token-value');

        try {
            let credential;
            let optionsUrl;
            const headers = {};

            if (requestTokenName && requestTokenValue) {
                headers[requestTokenName] = requestTokenValue;
            }

            // メールアドレスを取得（パスキー登録時に使用）
            let email = null;
            if (emailName) {
                const emailInput = form.querySelector(`[name="${emailName}"]`);
                if (emailInput) {
                    email = emailInput.value;
                }
            }

            if (operation === '0') {
                // Create: 新しいパスキーを登録
                optionsUrl = '/Account/PasskeyCreationOptions';
                const optionsResponse = await fetch(optionsUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        ...headers
                    },
                    body: JSON.stringify({ email })
                });

                if (!optionsResponse.ok) {
                    throw new Error('パスキー作成オプションの取得に失敗しました');
                }

                const options = await optionsResponse.json();
                
                // WebAuthn credentials.create を呼び出し
                const createOptions = this.parseCreationOptions(options);
                credential = await navigator.credentials.create(createOptions);
            } else {
                // Request: 既存のパスキーで認証
                optionsUrl = '/Account/PasskeyRequestOptions';
                const optionsResponse = await fetch(optionsUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        ...headers
                    },
                    body: JSON.stringify({ email })
                });

                if (!optionsResponse.ok) {
                    throw new Error('パスキー要求オプションの取得に失敗しました');
                }

                const options = await optionsResponse.json();
                
                // WebAuthn credentials.get を呼び出し
                const getOptions = this.parseRequestOptions(options);
                credential = await navigator.credentials.get(getOptions);
            }

            if (!credential) {
                throw new Error('パスキー操作がキャンセルされました');
            }

            // credential を JSON に変換
            const credentialJson = this.credentialToJson(credential);

            // フォームにクレデンシャルを設定
            this.setFormValue(form, name, 'CredentialJson', credentialJson);
            this.setFormValue(form, name, 'Error', '');

            // フォームを送信
            form.submit();

        } catch (error) {
            console.error('パスキー操作エラー:', error);
            this.setFormValue(form, name, 'CredentialJson', '');
            this.setFormValue(form, name, 'Error', error.message || 'パスキー操作に失敗しました');
            form.submit();
        }
    }

    parseCreationOptions(options) {
        return {
            publicKey: {
                challenge: this.base64UrlToArrayBuffer(options.challenge),
                rp: options.rp,
                user: {
                    id: this.base64UrlToArrayBuffer(options.user.id),
                    name: options.user.name,
                    displayName: options.user.displayName
                },
                pubKeyCredParams: options.pubKeyCredParams,
                timeout: options.timeout,
                attestation: options.attestation || 'none',
                authenticatorSelection: options.authenticatorSelection,
                excludeCredentials: (options.excludeCredentials || []).map(c => ({
                    id: this.base64UrlToArrayBuffer(c.id),
                    type: c.type,
                    transports: c.transports
                }))
            }
        };
    }

    parseRequestOptions(options) {
        return {
            publicKey: {
                challenge: this.base64UrlToArrayBuffer(options.challenge),
                timeout: options.timeout,
                rpId: options.rpId,
                allowCredentials: (options.allowCredentials || []).map(c => ({
                    id: this.base64UrlToArrayBuffer(c.id),
                    type: c.type,
                    transports: c.transports
                })),
                userVerification: options.userVerification || 'preferred'
            }
        };
    }

    credentialToJson(credential) {
        // PublicKeyCredential.toJSON が利用可能な場合は使用
        if (typeof credential.toJSON === 'function') {
            try {
                return JSON.stringify(credential.toJSON());
            } catch (e) {
                // toJSON が失敗した場合は手動でシリアライズ
                console.warn('credential.toJSON() failed, using manual serialization');
            }
        }

        // 手動シリアライズ（toJSON が利用できない場合のフォールバック）
        const response = credential.response;
        const result = {
            id: credential.id,
            rawId: this.arrayBufferToBase64Url(credential.rawId),
            type: credential.type,
            response: {}
        };

        if (response.attestationObject) {
            // 登録レスポンス
            result.response.attestationObject = this.arrayBufferToBase64Url(response.attestationObject);
            result.response.clientDataJSON = this.arrayBufferToBase64Url(response.clientDataJSON);
            if (response.getTransports) {
                result.response.transports = response.getTransports();
            }
        } else {
            // 認証レスポンス
            result.response.authenticatorData = this.arrayBufferToBase64Url(response.authenticatorData);
            result.response.clientDataJSON = this.arrayBufferToBase64Url(response.clientDataJSON);
            result.response.signature = this.arrayBufferToBase64Url(response.signature);
            if (response.userHandle) {
                result.response.userHandle = this.arrayBufferToBase64Url(response.userHandle);
            }
        }

        if (credential.authenticatorAttachment) {
            result.authenticatorAttachment = credential.authenticatorAttachment;
        }

        return JSON.stringify(result);
    }

    setFormValue(form, baseName, property, value) {
        const inputName = `${baseName}.${property}`;
        let input = form.querySelector(`input[name="${inputName}"]`);
        if (!input) {
            input = document.createElement('input');
            input.type = 'hidden';
            input.name = inputName;
            form.appendChild(input);
        }
        input.value = value;
    }

    base64UrlToArrayBuffer(base64Url) {
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const padding = '='.repeat((4 - base64.length % 4) % 4);
        const binary = atob(base64 + padding);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }

    arrayBufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }
}

customElements.define('passkey-submit', PasskeySubmit);
