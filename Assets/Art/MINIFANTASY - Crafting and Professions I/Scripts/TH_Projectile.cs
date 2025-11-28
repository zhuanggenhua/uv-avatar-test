using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Minifantasy.TrueHeroes
{
    public class TH_Projectile : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private bool hasThrowDistance = false;
        [SerializeField] private float throwDistance = 1f;
        [SerializeField] private float lifeTime = 2f;

        private Animator myAnimator;
        private Transform myTransform;
        private Rigidbody2D myRigidbody;

        private float totalDistance = 0f;
        private Vector3 previousPos;

        private void Awake()
        {
            myTransform = GetComponent<Transform>();
            myAnimator = GetComponent<Animator>();
            myRigidbody = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            setAnimator();
            Move();
            Vector2 startingPos = myRigidbody.position.normalized;
            StartCoroutine(SelfDestruct());
        }

        private void FixedUpdate()
        {
            if (hasThrowDistance)
            {
                RecordDistance();
                if (totalDistance > throwDistance) { setExplode(); }
            }
        }

        private void RecordDistance()
        {
            totalDistance += Vector3.Distance(myRigidbody.position, previousPos);
            previousPos = myRigidbody.position;
        }

        private void setExplode()
        {
            myRigidbody.linearVelocity = Vector2.zero;
            myAnimator.SetTrigger("Explode");
        }

        private void setAnimator()
        {
            myAnimator.SetFloat("X", myTransform.position.x);
            myAnimator.SetFloat("Y", myTransform.position.y);
        }

        private void Move()
        {
            myRigidbody.linearVelocity = new Vector2(myTransform.position.x * moveSpeed, myTransform.position.y * moveSpeed);
        }

        IEnumerator SelfDestruct()
        {
            yield return new WaitForSeconds(lifeTime);
            Destroy(gameObject);
        }
    }
}
